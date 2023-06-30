using System;
using System.Text.Json.Serialization;
using StabilityMatrix.Models;

namespace StabilityMatrix.Updater;

public class UpdateInfo
{
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

    public UpdateInfo(Version version, DateTimeOffset releaseDate, UpdateChannel channel, string downloadUrl, string changelogUrl, string? signature = null, UpdateType? updateType = null)
    {
        Version = version;
        ReleaseDate = releaseDate;
        Channel = channel;
        DownloadUrl = downloadUrl;
        ChangelogUrl = changelogUrl;
        Signature = signature;
        Type = updateType;
    }
}
