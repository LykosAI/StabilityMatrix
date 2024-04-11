using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Platform.Storage;
using StabilityMatrix.Core.Models.Api.OpenArt;

namespace StabilityMatrix.Avalonia.Models;

public class OpenArtMetadata
{
    [JsonPropertyName("sm_workflow_data")]
    public OpenArtSearchResult? Workflow { get; set; }

    [JsonIgnore]
    public string? FirstThumbnail => Workflow?.Thumbnails?.Select(x => x.Url).FirstOrDefault()?.ToString();

    [JsonIgnore]
    public List<IStorageFile>? FilePath { get; set; }

    [JsonIgnore]
    public bool HasMetadata => Workflow?.Creator != null;
}
