using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class ImageResponse
{
    [JsonPropertyName("images")]
    public string[] Images { get; set; }
    
    [JsonPropertyName("parameters")]
    public Dictionary<string, string>? Parameters { get; set; }
    
    [JsonPropertyName("info")]
    public string? Info { get; set; }
}
