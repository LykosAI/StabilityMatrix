namespace StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

public readonly record struct ComfyWebSocketImageData(byte[] ImageBytes, string? MimeType = null);
