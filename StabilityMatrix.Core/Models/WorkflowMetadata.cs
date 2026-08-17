using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.OpenArt;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Stability Matrix metadata embedded at the root of imported ComfyUI workflow files.
/// ComfyUI ignores unknown root-level keys, so the file stays loadable as a plain workflow.
/// </summary>
public class WorkflowMetadata
{
    /// <summary>
    /// Display data for the installed workflows library. The shape is the OpenArt
    /// search-result schema that historical imports embedded; other sources map into it.
    /// </summary>
    [JsonPropertyName("sm_workflow_data")]
    public OpenArtSearchResult? Workflow { get; set; }

    /// <summary>
    /// Web page this workflow was imported from, when the source site still has one.
    /// Null for legacy OpenArt imports, whose workflow pages no longer exist.
    /// </summary>
    [JsonPropertyName("sm_source_url")]
    public string? SourceUrl { get; set; }

    [JsonIgnore]
    public string? FirstThumbnail => Workflow?.Thumbnails?.Select(x => x.Url).FirstOrDefault()?.ToString();

    [JsonIgnore]
    [MemberNotNullWhen(true, nameof(Workflow))]
    public bool HasMetadata => Workflow?.Creator != null;
}
