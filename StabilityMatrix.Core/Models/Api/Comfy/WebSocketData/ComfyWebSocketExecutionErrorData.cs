namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public record ComfyWebSocketExecutionErrorData
{
    public required string PromptId { get; set; }
    public string? NodeId { get; set; }
    public string? NodeType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionType { get; set; }
    public string[]? Traceback { get; set; }
}
