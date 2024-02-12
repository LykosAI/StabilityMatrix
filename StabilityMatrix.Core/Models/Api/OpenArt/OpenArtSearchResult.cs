using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtSearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("creator")]
    public OpenArtCreator Creator { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("stats")]
    public OpenArtStats Stats { get; set; }

    [JsonPropertyName("nodes_index")]
    public IEnumerable<string> NodesIndex { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("categories")]
    public IEnumerable<string> Categories { get; set; }

    [JsonPropertyName("thumbnails")]
    public List<OpenArtThumbnail> Thumbnails { get; set; }

    [JsonPropertyName("nodes_count")]
    public NodesCount NodesCount { get; set; }
}
