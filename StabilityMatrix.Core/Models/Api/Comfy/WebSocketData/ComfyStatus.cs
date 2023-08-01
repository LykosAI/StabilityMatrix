using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public record struct ComfyStatus
{
    [JsonPropertyName("exec_info")]
    public required int ExecInfo { get; set; }
}
