using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitImage
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
    
    [JsonPropertyName("nsfw")]
    public string Nsfw { get; set; }
    
    [JsonPropertyName("width")]
    public int Width { get; set; }
    
    [JsonPropertyName("height")]
    public int Height { get; set; }
    
    [JsonPropertyName("hash")]
    public string Hash { get; set; }
    
    // TODO: "meta" ( object? )
}
