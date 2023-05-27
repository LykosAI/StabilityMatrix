using System.Collections.Generic;
using System.Text.Json.Serialization;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.Models.Api;

public class ImageResponse
{
    [JsonPropertyName("images")]
    public string[] Images { get; set; }
    
    // [JsonPropertyName("parameters")]
    // public TextToImageViewModel? Parameters { get; set; }
    
    [JsonPropertyName("info")]
    public string? Info { get; set; }
}
