using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.HuggingFace;

/// <summary>
/// A file or directory entry returned from the repository tree endpoint
/// (<c>GET /api/models/{owner}/{name}/tree/{revision}</c>).
/// </summary>
public record HuggingFaceTreeEntry
{
    /// <summary><c>"file"</c> or <c>"directory"</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Path of the entry relative to the repo root.</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>Size in bytes (0 for directories).</summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("oid")]
    public string? Oid { get; init; }

    [JsonIgnore]
    public bool IsFile => Type.Equals("file", System.StringComparison.OrdinalIgnoreCase);
}
