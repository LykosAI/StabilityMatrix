using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public record ComfyStatusExecInfo
{
    [JsonPropertyName("queue_remaining")]
    public required int QueueRemaining { get; set; }
}
