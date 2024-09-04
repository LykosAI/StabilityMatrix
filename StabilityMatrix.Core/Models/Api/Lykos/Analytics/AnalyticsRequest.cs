using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

public class AnalyticsRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public virtual string Type { get; set; } = "unknown";

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
