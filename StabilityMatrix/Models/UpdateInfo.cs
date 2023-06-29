using System.Text.Json.Serialization;

namespace StabilityMatrix.Models;

public class UpdateInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; }
    
    [JsonPropertyName("url")]
    public string DownloadUrl { get; set; }
    
    [JsonPropertyName("changelog")]
    public string ChangelogUrl { get; set; }
}
