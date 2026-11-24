using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Tests.Fakes;

/// <summary>
/// A local ComfyUI stand-in that serves a <see cref="ComfyRecording"/> on a free 127.0.0.1 port.
/// Each prompt posted to <c>/prompt</c> replays one recorded prompt, under the id the client
/// chose (or a new one, as ComfyUI does), to the client's websocket.
/// </summary>
public sealed class FakeComfyServer : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly ConcurrentDictionary<string, ClientSocket> sockets = new();
    private readonly ConcurrentDictionary<string, string> histories = new();
    private readonly List<Task> replays = [];

    private FakeComfyServer(ComfyRecording recording, WebApplication app)
    {
        Recording = recording;
        this.app = app;
        ObjectInfo = new ConcurrentDictionary<string, JsonObject>(
            recording.ObjectInfo.ToDictionary(p => p.Key, p => p.Value.DeepClone().AsObject())
        );
    }

    public ComfyRecording Recording { get; }

    public Uri BaseAddress { get; private set; } = null!;

    /// <summary>
    /// Bodies served at <c>/object_info/{node}</c>, editable per test. A node missing from it
    /// gets an empty object, as ComfyUI returns for a node it doesn't have.
    /// </summary>
    public ConcurrentDictionary<string, JsonObject> ObjectInfo { get; }

    /// <summary>Name of the recorded prompt replayed for each prompt posted.</summary>
    public string PromptScript { get; set; } = "success";

    /// <summary>
    /// Edits a replay's messages before they're sent. Takes the prompt's id and its messages,
    /// which already carry that id.
    /// </summary>
    public Func<string, IReadOnlyList<string>, IEnumerable<string>> EditReplay { get; set; } =
        (_, messages) => messages;

    /// <summary>
    /// How long <c>/prompt</c> holds its response after an ungated replay, so a client has read
    /// the replayed messages before it reads the response.
    /// </summary>
    public TimeSpan ResponseDelay { get; set; }

    /// <summary>
    /// When set, <c>/prompt</c> responds first and replays once this task completes. When null,
    /// every message is sent before the response, the earliest a real ComfyUI can send them.
    /// </summary>
    public Task? ReplayGate { get; set; }

    /// <summary>Assign a new prompt id instead of using the client's, as older ComfyUI versions do.</summary>
    public bool IgnoresClientPromptId { get; set; }

    /// <summary>Bodies posted to <c>/prompt</c>, in order.</summary>
    public ConcurrentQueue<JsonObject> ReceivedPrompts { get; } = new();

    public int InterruptCount => interruptCount;
    private int interruptCount;

    public static async Task<FakeComfyServer> StartAsync(ComfyRecording? recording = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var server = new FakeComfyServer(recording ?? ComfyRecording.Load(), builder.Build());
        server.MapRoutes();
        await server.app.StartAsync();

        var address = server
            .app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        server.BaseAddress = new Uri(address);
        return server;
    }

    /// <summary>Closes a client's websocket from the server side.</summary>
    public async Task DisconnectAsync(string clientId)
    {
        if (sockets.TryRemove(clientId, out var socket))
        {
            await socket.Socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, null, default);
        }
    }

    private void MapRoutes()
    {
        app.UseWebSockets();

        app.Map("/ws", HandleWebSocketAsync);

        app.MapGet("/object_info/{node}", (string node) =>
            Results.Text(
                ObjectInfo.TryGetValue(node, out var info) ? info.ToJsonString() : "{}",
                "application/json"
            )
        );

        app.MapPost("/prompt", HandlePromptAsync);

        app.MapGet("/history/{promptId}", (string promptId) =>
            Results.Text(histories.GetValueOrDefault(promptId, "{}"), "application/json")
        );

        // A missing filename query is a 400, as in ComfyUI
        app.MapGet("/view", (string filename) => Results.Bytes(Recording.ViewImage, "image/png"));

        app.MapPost("/interrupt", () =>
        {
            Interlocked.Increment(ref interruptCount);
            return Results.Ok();
        });
    }

    private async Task HandlePromptAsync(HttpContext context)
    {
        var body = (await JsonNode.ParseAsync(context.Request.Body))!.AsObject();
        ReceivedPrompts.Enqueue(body);

        var script = Recording.Prompts[PromptScript];
        if (script.PromptId is null)
        {
            context.Response.StatusCode = script.Status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(script.Response);
            return;
        }

        var clientPromptId = body["prompt_id"]?.GetValue<string>();
        var promptId =
            !IgnoresClientPromptId && clientPromptId is not null ? clientPromptId : Guid.NewGuid().ToString();
        var clientId = body["client_id"]?.GetValue<string>();

        var messages = EditReplay(
                promptId,
                script.Messages.Select(m => script.WithPromptId(m, promptId)).ToList()
            )
            .ToList();
        if (script.History is { } history)
        {
            histories[promptId] = script.WithPromptId(history, promptId);
        }

        var replay = ReplayAsync(clientId, messages, ReplayGate);
        if (ReplayGate is null)
        {
            await replay;
            await Task.Delay(ResponseDelay);
        }
        else
        {
            lock (replays)
            {
                replays.Add(replay);
            }
        }

        context.Response.StatusCode = script.Status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(script.WithPromptId(script.Response, promptId));
    }

    private async Task ReplayAsync(string? clientId, IReadOnlyList<string> messages, Task? gate)
    {
        if (gate is not null)
        {
            await gate;
        }

        // ComfyUI sends a prompt's messages only to the client that queued it
        if (clientId is null)
        {
            return;
        }

        var socket = await GetSocketAsync(clientId);
        foreach (var message in messages)
        {
            await socket.SendAsync(message);
        }
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var clientId = context.Request.Query["clientId"].ToString();
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = Guid.NewGuid().ToString("N");
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var socket = new ClientSocket(webSocket);

        var status = Recording.ConnectStatus.DeepClone();
        status["data"]!["sid"] = clientId;
        await socket.SendAsync(status.ToJsonString());

        sockets[clientId] = socket;

        var buffer = new byte[4096];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, default);
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException) { }
        finally
        {
            sockets.TryRemove(new KeyValuePair<string, ClientSocket>(clientId, socket));
        }
    }

    // A client's websocket handshake can finish before this server registers the socket, and a
    // client that lost its websocket reconnects on its own, so sends wait for an open socket.
    private static readonly TimeSpan SocketWait = TimeSpan.FromSeconds(30);

    private async Task<ClientSocket> GetSocketAsync(string clientId)
    {
        var deadline = DateTime.UtcNow + SocketWait;
        while (true)
        {
            if (sockets.TryGetValue(clientId, out var socket) && socket.Socket.State == WebSocketState.Open)
            {
                return socket;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Client {clientId} has no open websocket");
            }

            await Task.Delay(20);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task[] pending;
        lock (replays)
        {
            pending = [.. replays];
        }
        // A gated replay the test never released, or one whose client went away, mustn't hold up disposal
        try
        {
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception e) when (e is TimeoutException or TaskCanceledException or WebSocketException) { }

        await app.StopAsync();
        await app.DisposeAsync();
    }

    private sealed class ClientSocket(WebSocket socket)
    {
        private readonly SemaphoreSlim sendLock = new(1, 1);

        public WebSocket Socket => socket;

        public async Task SendAsync(string message)
        {
            await sendLock.WaitAsync();
            try
            {
                await socket.SendAsync(
                    Encoding.UTF8.GetBytes(message),
                    WebSocketMessageType.Text,
                    true,
                    default
                );
            }
            finally
            {
                sendLock.Release();
            }
        }
    }
}
