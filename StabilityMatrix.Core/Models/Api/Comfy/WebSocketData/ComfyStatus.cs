using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public record ComfyStatus
{
    [JsonPropertyName("exec_info")]
    public required ComfyStatusExecInfo ExecInfo { get; set; }
}
