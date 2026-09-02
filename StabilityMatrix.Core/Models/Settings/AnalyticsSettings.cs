using System.Text.Json.Serialization;
using Semver;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Settings;

public class AnalyticsSettings
{
    /// <summary>
    /// Revision of the consent dialog. Bump it whenever the set of categories offered changes so
    /// that users who already opted in see the updated dialog once.
    /// </summary>
    public const int CurrentConsentRevision = 2;

    [JsonIgnore]
    public static TimeSpan DefaultLaunchDataSendInterval { get; set; } = TimeSpan.FromDays(1);

    [JsonConverter(typeof(SemVersionJsonConverter))]
    public SemVersion? LastSeenConsentVersion { get; set; }

    public bool? LastSeenConsentAccepted { get; set; }

    /// <summary>
    /// The consent dialog revision the user last answered. Null means the original single-toggle
    /// dialog (revision 1).
    /// </summary>
    public int? LastSeenConsentRevision { get; set; }

    /// <summary>
    /// Master switch. When off, nothing is sent regardless of the per-category flags.
    /// </summary>
    public bool IsUsageDataEnabled { get; set; }

    /// <summary>
    /// Random identifier that lets installations be counted without identifying anyone. Exists
    /// only while usage data is enabled; cleared on opt-out and replaceable by the user.
    /// </summary>
    public Guid? InstallationId { get; set; }

    public bool IsHardwareDataEnabled { get; set; }

    public HardwareDetailLevel HardwareDetailLevel { get; set; } = HardwareDetailLevel.Detailed;

    public bool IsPackageUsageDataEnabled { get; set; }

    public bool IsInferenceUsageDataEnabled { get; set; }

    public bool IsFeatureUsageDataEnabled { get; set; }

    public bool IsPackageInstallDataEnabled { get; set; }

    public DateTimeOffset? LaunchDataLastSentAt { get; set; }

    /// <summary>
    /// True when the user has never answered the consent dialog.
    /// </summary>
    [JsonIgnore]
    public bool IsConsentUnanswered => LastSeenConsentVersion is null || LastSeenConsentAccepted is null;

    /// <summary>
    /// True when the user opted in under an older consent revision and should see the updated
    /// dialog once. Users who declined are not asked again.
    /// </summary>
    [JsonIgnore]
    public bool IsConsentOutdated =>
        !IsConsentUnanswered && IsUsageDataEnabled && (LastSeenConsentRevision ?? 1) < CurrentConsentRevision;

    /// <summary>
    /// Whether data of the given category may be sent: requires both the master switch and the
    /// category's own flag.
    /// </summary>
    public bool IsCategoryEnabled(AnalyticsCategory category) =>
        IsUsageDataEnabled && GetCategoryFlag(category);

    /// <summary>
    /// The category's own flag, regardless of the master switch.
    /// </summary>
    public bool GetCategoryFlag(AnalyticsCategory category) =>
        category switch
        {
            AnalyticsCategory.Hardware => IsHardwareDataEnabled,
            AnalyticsCategory.PackageUsage => IsPackageUsageDataEnabled,
            AnalyticsCategory.InferenceUsage => IsInferenceUsageDataEnabled,
            AnalyticsCategory.FeatureUsage => IsFeatureUsageDataEnabled,
            AnalyticsCategory.PackageInstalls => IsPackageInstallDataEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };

    public void SetCategoryFlag(AnalyticsCategory category, bool enabled)
    {
        switch (category)
        {
            case AnalyticsCategory.Hardware:
                IsHardwareDataEnabled = enabled;
                break;
            case AnalyticsCategory.PackageUsage:
                IsPackageUsageDataEnabled = enabled;
                break;
            case AnalyticsCategory.InferenceUsage:
                IsInferenceUsageDataEnabled = enabled;
                break;
            case AnalyticsCategory.FeatureUsage:
                IsFeatureUsageDataEnabled = enabled;
                break;
            case AnalyticsCategory.PackageInstalls:
                IsPackageInstallDataEnabled = enabled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, null);
        }
    }
}
