using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public record ComfyWebSocketProgressData
{
    [JsonPropertyName("value")]
    public required int Value { get; set; }
    
    [JsonPropertyName("max")]
    public required int Max { get; set; }
}
