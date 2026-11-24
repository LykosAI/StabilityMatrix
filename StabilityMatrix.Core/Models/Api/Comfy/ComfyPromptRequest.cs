using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyPromptRequest
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }

    /// <summary>
    /// Id for the server to give this prompt. ComfyUI versions without client-chosen ids
    /// ignore it and assign their own, returned in <see cref="ComfyPromptResponse.PromptId"/>.
    /// </summary>
    [JsonPropertyName("prompt_id")]
    public string? PromptId { get; set; }

    [JsonPropertyName("prompt")]
    public required Dictionary<string, ComfyNode> Prompt { get; set; }
}
