using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyImage
{
    [JsonPropertyName("filename")]
    public required string FileName { get; set; }
    
    [JsonPropertyName("subfolder")]
    public required string SubFolder { get; set; }
    
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}
