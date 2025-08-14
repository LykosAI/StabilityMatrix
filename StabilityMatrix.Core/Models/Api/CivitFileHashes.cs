using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public record CivitFileHashes
{
    public string? SHA256 { get; set; }

    public string? CRC32 { get; set; }

    public string? BLAKE3 { get; set; }

    [JsonIgnore]
    public string ShortSha256 => SHA256?[..8] ?? string.Empty;

    [JsonIgnore]
    public string ShortBlake3 => BLAKE3?[..8] ?? string.Empty;
}
