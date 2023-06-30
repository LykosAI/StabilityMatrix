using System;
using System.Text.Json.Serialization;
using StabilityMatrix.Models;

namespace StabilityMatrix.Updater;

public class UpdateInfo
{
#pragma warning disable CS8618
    [JsonRequired]
    [JsonPropertyName("version")]
    public Version Version { get; set; }
    
    [JsonRequired]
    [JsonPropertyName("releaseDate")]
    public DateTimeOffset ReleaseDate { get; set; }
    
    [JsonRequired]
    [JsonPropertyName("channel")]
    public UpdateChannel Channel { get; set; }

    [JsonRequired]
    [JsonPropertyName("url")]
    public string DownloadUrl { get; set; }
    
    [JsonRequired]
    [JsonPropertyName("changelog")]
    public string ChangelogUrl { get; set; }
    
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
    
    [JsonPropertyName("type")]
    public UpdateType? Type { get; set; }
#pragma warning restore CS8618
}
