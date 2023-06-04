using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitFileMetadata
{
    [JsonPropertyName("fp")]
    public CivitModelFpType? Fp { get; set; }
    
    [JsonPropertyName("size")]
    public CivitModelSize? Size { get; set; }
    
    [JsonPropertyName("format")]
    public CivitModelFormat? Format { get; set; }
}
