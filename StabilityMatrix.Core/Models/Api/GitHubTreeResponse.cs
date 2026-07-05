using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

/// <summary>
/// Response from the GitHub <c>git/trees</c> API (recursive listing).
/// </summary>
public record GitHubTreeResponse
{
    [JsonPropertyName("sha")]
    public string? Sha { get; init; }

    [JsonPropertyName("tree")]
    public IReadOnlyList<GitHubTreeItem> Tree { get; init; } = [];

    /// <summary>
    /// Whether the returned tree was truncated by the API (too many entries).
    /// </summary>
    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }
}

/// <summary>
/// A single entry in a GitHub git tree.
/// </summary>
public record GitHubTreeItem
{
    /// <summary>Path relative to the tree root (e.g. <c>getting-started/overview.md</c>).</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>Either <c>blob</c> (file) or <c>tree</c> (directory).</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("sha")]
    public string? Sha { get; init; }

    [JsonIgnore]
    public bool IsBlob => Type == "blob";
}
