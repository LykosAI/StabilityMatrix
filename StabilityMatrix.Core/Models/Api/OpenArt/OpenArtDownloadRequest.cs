using System.Text.Json.Serialization;
using Refit;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtDownloadRequest
{
    [AliasAs("workflow_id")]
    [JsonPropertyName("workflow_id")]
    public required string WorkflowId { get; set; }

    [AliasAs("version_tag")]
    [JsonPropertyName("version_tag")]
    public string VersionTag { get; set; } = "latest";
}
