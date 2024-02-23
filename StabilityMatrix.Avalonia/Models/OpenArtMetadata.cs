using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Platform.Storage;
using StabilityMatrix.Avalonia;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtMetadata
{
    [JsonPropertyName("workflow_id")]
    public string? Id { get; set; }

    [JsonPropertyName("workflow_name")]
    public string? Name { get; set; }

    [JsonPropertyName("creator")]
    public string? Creator { get; set; }

    [JsonPropertyName("thumbnails")]
    public IEnumerable<string>? ThumbnailUrls { get; set; }

    [JsonIgnore]
    public string? FirstThumbnail => ThumbnailUrls?.FirstOrDefault();

    [JsonIgnore]
    public List<IStorageFile>? FilePath { get; set; }
}
