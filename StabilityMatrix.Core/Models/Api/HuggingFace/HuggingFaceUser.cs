using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.HuggingFace;

public record HuggingFaceUser
{
    [JsonPropertyName("name")]
    public string? Name { get; init; } // Typically the username

    [JsonPropertyName("orgs")]
    public List<HuggingFaceOrg>? Orgs { get; init; }

    // Add other fields if the API explorer (https://huggingface.co/spaces/enzostvs/hub-api-playground)
    // or further documentation reveals more useful fields like email, id, etc.
    // For now, 'name' is the most important for the current task.
}

public record HuggingFaceOrg
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    // Add other org fields if necessary
}
