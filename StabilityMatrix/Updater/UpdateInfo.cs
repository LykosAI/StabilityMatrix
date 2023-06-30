using System;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Updater;

public record UpdateInfo(
    [property: JsonPropertyName("version")]
    Version Version,
    
    [property: JsonPropertyName("releaseDate")]
    DateTimeOffset ReleaseDate,
    
    [property: JsonPropertyName("channel")]
    UpdateChannel Channel,
    
    [property: JsonPropertyName("url")] 
    string DownloadUrl,
    
    [property: JsonPropertyName("changelog")]
    string ChangelogUrl,
    
    [property: JsonPropertyName("signature")]
    string Signature,
    
    [property: JsonPropertyName("type")] 
    UpdateType Type
);
