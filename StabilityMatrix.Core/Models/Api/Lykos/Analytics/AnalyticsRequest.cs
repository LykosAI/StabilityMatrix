using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class AnalyticsRequest
{
    [JsonPropertyName("type")]
    public virtual string Type { get; set; } = "unknown";

    /// <summary>
    /// Random per-installation identifier, stamped on every request while usage data is enabled.
    /// </summary>
    public Guid? InstallationId { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
