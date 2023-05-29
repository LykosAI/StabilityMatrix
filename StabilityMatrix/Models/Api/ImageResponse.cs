using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class ImageResponse
{
    [JsonPropertyName("images")]
    public string[] Images { get; set; }

    [JsonPropertyName("info")]
    public string? Info { get; set; }
}
