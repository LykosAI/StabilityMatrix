using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public record ComfyWebSocketStatusData
{
    [JsonPropertyName("status")]
    public required ComfyStatus Status { get; set; }
}
