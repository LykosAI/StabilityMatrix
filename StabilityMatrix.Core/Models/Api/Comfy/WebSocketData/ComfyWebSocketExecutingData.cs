using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public class ComfyWebSocketExecutingData
{
    [JsonPropertyName("prompt_id")]
    public required string PromptId { get; set; }
    
    /// <summary>
    /// When this is null it indicates completed
    /// </summary>
    [JsonPropertyName("node")]
    public required string? Node { get; set; }
}
