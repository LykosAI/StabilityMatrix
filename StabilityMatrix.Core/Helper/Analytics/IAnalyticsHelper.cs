using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Helper.Analytics;

/// <summary>
/// Sends opt-in anonymous usage data and owns the consent lifecycle: the master switch, the
/// per-category flags and the installation id that goes with them.
/// </summary>
public interface IAnalyticsHelper
{
    /// <summary>
    /// Records the user's answer to the consent dialog, enabling exactly the given categories
    /// when accepted and clearing everything (including the installation id) when declined.
    /// </summary>
    void ApplyConsent(bool accepted, IReadOnlyCollection<AnalyticsCategory> enabledCategories);

    /// <summary>
    /// Flips the master switch. Turning it off removes the installation id; turning it on creates
    /// a fresh one, so re-enabling never links back to earlier data.
    /// </summary>
    void SetUsageDataEnabled(bool enabled);

    void SetCategoryEnabled(AnalyticsCategory category, bool enabled);

    /// <summary>Replaces the installation id with a new random one (no-op while disabled).</summary>
    void ResetInstallationId();

    Task TrackPackageInstallAsync(string packageName, string packageVersion, bool isSuccess);

    Task TrackFirstTimeInstallAsync(
        string? selectedPackageName,
        IEnumerable<string>? selectedRecommendedModels,
        bool firstTimeSetupSkipped
    );

    Task TrackAsync(AnalyticsRequest data);

    /// <summary>
    /// Sends the launch ping plus the hardware and aggregated usage reports for the enabled
    /// categories, at most once per <see cref="AnalyticsSettings.DefaultLaunchDataSendInterval"/>.
    /// Safe to fire and forget; failures are logged and retried on a later call.
    /// </summary>
    Task SendPeriodicReportsIfDueAsync();
}
