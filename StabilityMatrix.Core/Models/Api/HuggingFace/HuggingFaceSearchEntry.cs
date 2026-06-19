using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.HuggingFace;

/// <summary>
/// A single model entry returned from the HuggingFace model search endpoint
/// (<c>GET /api/models</c>).
/// </summary>
public record HuggingFaceSearchEntry
{
    /// <summary>Full repository id, e.g. <c>"owner/name"</c>.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("downloads")]
    public long Downloads { get; init; }

    [JsonPropertyName("likes")]
    public long Likes { get; init; }

    [JsonPropertyName("pipeline_tag")]
    public string? PipelineTag { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    [JsonPropertyName("lastModified")]
    public DateTimeOffset? LastModified { get; init; }
}
