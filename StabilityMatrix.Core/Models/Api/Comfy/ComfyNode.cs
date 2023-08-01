using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyNode
{
    [JsonPropertyName("class_type")]
    public required string ClassType;
    
    [JsonPropertyName("inputs")]
    public required Dictionary<string, object?> Inputs;
}
