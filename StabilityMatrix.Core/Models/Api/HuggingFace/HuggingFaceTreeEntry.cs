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

    /// <summary>
    /// LFS metadata, present only for files stored in Git LFS (i.e. the actual model weights).
    /// Small files committed directly to git (configs, tokenizers) have no LFS entry.
    /// </summary>
    [JsonPropertyName("lfs")]
    public HuggingFaceLfsInfo? Lfs { get; init; }

    [JsonIgnore]
    public bool IsFile => Type.Equals("file", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Content SHA256 of the file, or null when not an LFS file. Unlike <see cref="Oid"/>
    /// (the git blob SHA-1), this is the hash of the file's actual bytes, so it can be matched
    /// against the local model index to detect an already-installed file regardless of its name
    /// or folder. The tree API returns the LFS oid as a bare hex digest; git-LFS pointer files
    /// prefix it with <c>sha256:</c>, so we tolerate (and strip) that prefix if present.
    /// </summary>
    [JsonIgnore]
    public string? Sha256 =>
        Lfs?.Oid is { Length: > 0 } oid
            ? (
                oid.StartsWith("sha256:", System.StringComparison.OrdinalIgnoreCase)
                    ? oid["sha256:".Length..]
                    : oid
            )
            : null;
}

/// <summary>LFS pointer metadata for a single tree entry.</summary>
public record HuggingFaceLfsInfo
{
    /// <summary>Content SHA256, returned by the tree API as a bare hex digest (no <c>sha256:</c> prefix).</summary>
    [JsonPropertyName("oid")]
    public string? Oid { get; init; }

    /// <summary>Size of the actual (LFS-resolved) file in bytes.</summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }
}
