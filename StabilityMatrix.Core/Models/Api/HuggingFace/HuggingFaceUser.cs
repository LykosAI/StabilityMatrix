using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.HuggingFace;

public record HuggingFaceUser
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
