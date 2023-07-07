using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class CivitCreator
{
    [JsonPropertyName("username")]
    public string Username { get; set; }
    
    [JsonPropertyName("image")]
    public string? Image { get; set; }
}
