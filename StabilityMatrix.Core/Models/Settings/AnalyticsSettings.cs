using System.Text.Json.Serialization;
using Semver;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Settings;

public class AnalyticsSettings
{
    [JsonIgnore]
    public static TimeSpan DefaultLaunchDataSendInterval { get; set; } = TimeSpan.FromDays(1);

    [JsonConverter(typeof(SemVersionJsonConverter))]
    public SemVersion? LastSeenConsentVersion { get; set; }

    public bool? LastSeenConsentAccepted { get; set; }

    public bool IsUsageDataEnabled { get; set; }

    public DateTimeOffset? LaunchDataLastSentAt { get; set; }
}
