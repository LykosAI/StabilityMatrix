using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class CivitFileMetadata
{
    [JsonPropertyName("fp")]
    public string? Fp { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("format")]
    public CivitModelFormat? Format { get; set; }
}
