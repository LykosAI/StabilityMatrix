using System;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models;

public class UpdateInfo
{
    [JsonPropertyName("version")]
    public Version Version { get; set; }
    
    [JsonPropertyName("url")]
    public string DownloadUrl { get; set; }
    
    [JsonPropertyName("changelog")]
    public string ChangelogUrl { get; set; }
}
