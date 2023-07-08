using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Update;

public record UpdateInfo(
    [property: JsonPropertyName("version")]
    Version Version,
    
    [property: JsonPropertyName("releaseDate")]
    DateTimeOffset ReleaseDate,
    
    [property: JsonPropertyName("channel")]
    UpdateChannel Channel,
    
    [property: JsonPropertyName("type")] 
    UpdateType Type,
    
    [property: JsonPropertyName("url")] 
    string DownloadUrl,
    
    [property: JsonPropertyName("changelog")]
    string ChangelogUrl,
    
    // Blake3 hash of the file
    [property: JsonPropertyName("hashBlake3")]
    string HashBlake3,
    
    // ED25519 signature of the semicolon seperated string:
    // "version + releaseDate + channel + type + url + changelog + hash_blake3"
    // verifiable using our stored public key
    [property: JsonPropertyName("signature")]
    string Signature
);
