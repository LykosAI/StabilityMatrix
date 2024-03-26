using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class NodesCount
{
    [JsonPropertyName("total")]
    public long Total { get; set; }

    [JsonPropertyName("primitive")]
    public long Primitive { get; set; }

    [JsonPropertyName("custom")]
    public long Custom { get; set; }
}
