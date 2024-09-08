using System.Text.Json.Serialization;
using Semver;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Core.Models.Settings;

public class AnalyticsSettings
{
    [JsonConverter(typeof(SemVersionJsonConverter))]
    public SemVersion? LastSeenConsentVersion { get; set; }

    public bool? LastSeenConsentAccepted { get; set; }

    public bool IsUsageDataEnabled { get; set; }
}
