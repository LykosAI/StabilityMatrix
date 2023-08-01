using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Core.Inference;

/// <summary>
/// Websocket client for Comfy inference server
/// Connects to localhost:8188 by default
/// </summary>
public class ComfyWebSocketClient : IDisposable
{
    private bool isDisposed;
    private readonly ClientWebSocket clientWebSocket = new();
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly CancellationToken cancellationToken;
    private readonly JsonSerializerOptions? jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ComfyWebSocketClient()
    {
        cancellationToken = cancellationTokenSource.Token;
    }

    public async Task ConnectAsync(Uri baseAddress, string clientId)
    {
        var uri = new UriBuilder(baseAddress)
        {
            Path = "/ws",
            Query = $"client_id={clientId}"
        }.Uri;
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
    
    public async Task<ComfyWebSocketResponseUnion?> ReceiveAsync()
    {
        var shared = ArrayPool<byte>.Shared;
        var buffer = shared.Rent(1024);
        try
        {
            var result = await clientWebSocket
                .ReceiveAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            if (result.MessageType is WebSocketMessageType.Binary)
            {
                return new ComfyWebSocketResponseUnion
                {
                    MessageType = result.MessageType,
                    Json = null,
                    Bytes = buffer.AsSpan(0, result.Count).ToArray()
                };
            }
            else
            {
                var text = Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count));
                var json = JsonSerializer.Deserialize<ComfyWebSocketResponse>(text, jsonSerializerOptions);
                return new ComfyWebSocketResponseUnion
                {
                    MessageType = result.MessageType,
                    Json = json,
                    Bytes = null
                };
            }
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
        if (isDisposed) return;
        clientWebSocket.Dispose();
        cancellationTokenSource.Dispose();
        isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
