using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

// ReSharper disable once ClassNeverInstantiated.Global
public class ComfyPromptResponse
{
    [JsonPropertyName("prompt_id")]
    public required string PromptId { get; set; }

    [JsonPropertyName("number")]
    public required int Number { get; set; }
    
    [JsonPropertyName("node_errors")]
    public required Dictionary<string, object?> NodeErrors { get; set; }
}
