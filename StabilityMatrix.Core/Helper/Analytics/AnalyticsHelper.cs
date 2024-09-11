using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Helper.Analytics;

[Singleton(typeof(IAnalyticsHelper))]
public class AnalyticsHelper(
    ILogger<AnalyticsHelper> logger,
    ILykosAnalyticsApi analyticsApi,
    ISettingsManager settingsManager
) : IAnalyticsHelper
{
    public AnalyticsSettings Settings => settingsManager.Settings.Analytics;

    public async Task TrackPackageInstallAsync(string packageName, string packageVersion, bool isSuccess)
    {
        if (!Settings.IsUsageDataEnabled)
        {
            return;
        }

        var data = new PackageInstallAnalyticsRequest
        {
            PackageName = packageName,
            PackageVersion = packageVersion,
            IsSuccess = isSuccess,
            Timestamp = DateTimeOffset.UtcNow
        };

        try
        {
            await analyticsApi.PostInstallData(data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending install data");
        }
    }

    public async Task TrackFirstTimeInstallAsync(
        string? selectedPackageName,
        IEnumerable<string>? selectedRecommendedModels,
        bool firstTimeSetupSkipped,
        string platform
    )
    {
        if (!Settings.IsUsageDataEnabled)
        {
            return;
        }

        var data = new FirstTimeInstallAnalytics
        {
            SelectedPackageName = selectedPackageName,
            SelectedRecommendedModels = selectedRecommendedModels,
            FirstTimeSetupSkipped = firstTimeSetupSkipped,
            Platform = platform,
            Timestamp = DateTimeOffset.UtcNow
        };

        try
        {
            await analyticsApi.PostInstallData(data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending first time install data");
        }
    }

    public async Task TrackAsync(AnalyticsRequest data)
    {
        if (!Settings.IsUsageDataEnabled)
        {
            return;
        }

        try
        {
            await analyticsApi.PostInstallData(data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending analytics data");
        }
    }
}
