using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Inference;

/// <summary>
/// Websocket client for Comfy inference server
/// Connects to localhost:8188 by default
/// </summary>
public class ComfyClient : IInferenceClient
{
    private readonly ClientWebSocket clientWebSocket = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly CancellationToken cancellationToken;
    private readonly JsonSerializerOptions? jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    
    protected Guid ClientId { get; } = Guid.NewGuid();

    public ComfyClient()
    {
        cancellationToken = cancellationTokenSource.Token;
    }

    public async Task ConnectAsync(Uri uri)
    {
        await clientWebSocket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendAsync<T>(T message)
    {
        var json = JsonSerializer.Serialize(message, jsonSerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var buffer = new ArraySegment<byte>(bytes);
        await clientWebSocket
            .SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<T?> ReceiveAsync<T>()
    {
        var shared = ArrayPool<byte>.Shared;
        var buffer = shared.Rent(1024);
        try
        {
            var result = await clientWebSocket
                .ReceiveAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            return JsonSerializer.Deserialize<T>(json, jsonSerializerOptions);
        }
        finally
        {
            shared.Return(buffer);
        }
    }

    public async Task CloseAsync()
    {
        await clientWebSocket
            .CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        clientWebSocket.Dispose();
        cancellationTokenSource.Dispose();
    }
}
