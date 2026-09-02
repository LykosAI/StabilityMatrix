using System.Runtime.InteropServices;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Helper.Analytics;

[RegisterSingleton<IAnalyticsHelper, AnalyticsHelper>]
public class AnalyticsHelper(
    ILogger<AnalyticsHelper> logger,
    ILykosAnalyticsApi analyticsApi,
    ISettingsManager settingsManager,
    IAnalyticsUsageTracker usageTracker
) : IAnalyticsHelper
{
    public AnalyticsSettings Settings => settingsManager.Settings.Analytics;

    public void ApplyConsent(bool accepted, IReadOnlyCollection<AnalyticsCategory> enabledCategories)
    {
        settingsManager.Transaction(s =>
        {
            var analytics = s.Analytics;

            analytics.LastSeenConsentVersion = Compat.AppVersion;
            analytics.LastSeenConsentAccepted = accepted;
            analytics.LastSeenConsentRevision = AnalyticsSettings.CurrentConsentRevision;
            analytics.IsUsageDataEnabled = accepted;

            foreach (var category in Enum.GetValues<AnalyticsCategory>())
            {
                analytics.SetCategoryFlag(category, accepted && enabledCategories.Contains(category));
            }

            analytics.InstallationId = accepted ? analytics.InstallationId ?? Guid.NewGuid() : null;
        });
    }

    public void SetUsageDataEnabled(bool enabled)
    {
        settingsManager.Transaction(s =>
        {
            s.Analytics.IsUsageDataEnabled = enabled;
            s.Analytics.InstallationId = enabled ? Guid.NewGuid() : null;
        });
    }

    public void SetCategoryEnabled(AnalyticsCategory category, bool enabled)
    {
        settingsManager.Transaction(s => s.Analytics.SetCategoryFlag(category, enabled));
    }

    public void ResetInstallationId()
    {
        if (!Settings.IsUsageDataEnabled)
        {
            return;
        }

        settingsManager.Transaction(s => s.Analytics.InstallationId = Guid.NewGuid());
    }

    public Task TrackPackageInstallAsync(string packageName, string packageVersion, bool isSuccess)
    {
        if (!Settings.IsCategoryEnabled(AnalyticsCategory.PackageInstalls))
        {
            return Task.CompletedTask;
        }

        return TrySendAsync(
            new PackageInstallAnalyticsRequest
            {
                PackageName = packageName,
                PackageVersion = packageVersion,
                IsSuccess = isSuccess,
                Timestamp = DateTimeOffset.UtcNow,
            }
        );
    }

    public Task TrackFirstTimeInstallAsync(
        string? selectedPackageName,
        IEnumerable<string>? selectedRecommendedModels,
        bool firstTimeSetupSkipped
    )
    {
        if (!Settings.IsCategoryEnabled(AnalyticsCategory.PackageInstalls))
        {
            return Task.CompletedTask;
        }

        return TrySendAsync(
            new FirstTimeInstallAnalytics
            {
                SelectedPackageName = selectedPackageName,
                SelectedRecommendedModels = selectedRecommendedModels,
                FirstTimeSetupSkipped = firstTimeSetupSkipped,
                Timestamp = DateTimeOffset.UtcNow,
            }
        );
    }

    public Task TrackAsync(AnalyticsRequest data)
    {
        return Settings.IsUsageDataEnabled ? TrySendAsync(data) : Task.CompletedTask;
    }

    public async Task SendPeriodicReportsIfDueAsync()
    {
        if (!Settings.IsUsageDataEnabled || !IsPeriodicReportDue())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var launch = new LaunchAnalyticsRequest
        {
            Version = Compat.AppVersion.ToString(),
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            OsDescription = RuntimeInformation.OSDescription,
            Timestamp = now,
        };

        // The launch ping doubles as the connectivity probe: if it fails, everything waits for
        // the next launch instead of producing a half-delivered day.
        if (!await TrySendAsync(launch).ConfigureAwait(false))
        {
            return;
        }

        settingsManager.Transaction(s => s.Analytics.LaunchDataLastSentAt = now);

        if (Settings.IsCategoryEnabled(AnalyticsCategory.Hardware))
        {
            var hardware = await Task.Run(HardwareAnalyticsRequest.FromCurrentSystem).ConfigureAwait(false);
            await TrySendAsync(hardware).ConfigureAwait(false);
        }

        foreach (var (category, report) in usageTracker.BuildReports(now))
        {
            if (await TrySendAsync(report).ConfigureAwait(false))
            {
                usageTracker.ResetCategory(category);
            }
        }
    }

    private bool IsPeriodicReportDue() =>
        Settings.LaunchDataLastSentAt is not { } lastSentAt
        || DateTimeOffset.UtcNow - lastSentAt > AnalyticsSettings.DefaultLaunchDataSendInterval;

    /// <summary>
    /// Stamps the installation id and posts the request, returning false instead of throwing.
    /// </summary>
    private async Task<bool> TrySendAsync(AnalyticsRequest data)
    {
        data.InstallationId ??= GetOrCreateInstallationId();

        try
        {
            await analyticsApi.PostInstallData(data).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending analytics data of type {Type}", data.Type);
            return false;
        }
    }

    /// <summary>
    /// Installations that opted in before ids existed get one on their first send.
    /// </summary>
    private Guid? GetOrCreateInstallationId()
    {
        if (!Settings.IsUsageDataEnabled)
        {
            return null;
        }

        if (Settings.InstallationId is { } existing)
        {
            return existing;
        }

        var id = Guid.NewGuid();
        settingsManager.Transaction(s => s.Analytics.InstallationId = id);
        return id;
    }
}
