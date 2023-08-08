using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using NLog;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;
using StabilityMatrix.Core.Models.FileInterfaces;
using Websocket.Client;

namespace StabilityMatrix.Core.Inference;

public class ComfyClient : InferenceClientBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly WebsocketClient webSocketClient;
    private readonly IComfyApi comfyApi;
    private bool isDisposed;

    // ReSharper disable once MemberCanBePrivate.Global
    public string ClientId { get; } = Guid.NewGuid().ToString();
    
    public Uri BaseAddress { get; }
    
    /// <summary>
    /// Optional local path to output images.
    /// </summary>
    public DirectoryPath? OutputImagesDir { get; set; }

    /// <summary>
    /// Dictionary of ongoing prompt execution tasks
    /// </summary>
    public ConcurrentDictionary<string, TaskCompletionSource> PromptTasks { get; } = new();
    
    /// <summary>
    /// Event raised when a progress update is received from the server
    /// </summary>
    public event EventHandler<ComfyWebSocketProgressData>? ProgressUpdateReceived;
    
    /// <summary>
    /// Event raised when a status update is received from the server
    /// </summary>
    public event EventHandler<ComfyWebSocketStatusData>? StatusUpdateReceived;
    
    /// <summary>
    /// Event raised when a executing update is received from the server
    /// </summary>
    public event EventHandler<ComfyWebSocketExecutingData>? ExecutingUpdateReceived;

    /// <summary>
    /// Event raised when a preview image is received from the server
    /// </summary>
    public event EventHandler<ComfyWebSocketImageData>? PreviewImageReceived; 
    
    public ComfyClient(IApiFactory apiFactory, Uri baseAddress)
    {
        comfyApi = apiFactory.CreateRefitClient<IComfyApi>(baseAddress);
        BaseAddress = baseAddress;

        // Setup websocket client
        var wsUri = new UriBuilder(baseAddress)
        {
            Scheme = "ws",
            Path = "/ws",
            Query = $"clientId={ClientId}"
        }.Uri;
        webSocketClient = new WebsocketClient(wsUri)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
        };
        
        webSocketClient.DisconnectionHappened.Subscribe(
            info => Logger.Info("Websocket Disconnected, ({Type})", info.Type)
        );
        webSocketClient.ReconnectionHappened.Subscribe(
            info => Logger.Info("Websocket Reconnected, ({Type})", info.Type)
        );

        webSocketClient.MessageReceived.Subscribe(OnMessageReceived);
    }

    private void OnMessageReceived(ResponseMessage message)
    {
        switch (message.MessageType)
        {
            case WebSocketMessageType.Text:
                HandleTextMessage(message.Text);
                break;
            case WebSocketMessageType.Binary:
                HandleBinaryMessage(message.Binary);
                break;
            case WebSocketMessageType.Close:
                Logger.Trace("Received ws close message: {Text}", message.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(message));
        }
    }

    private void HandleTextMessage(string text)
    {
        ComfyWebSocketResponse? json;
        try
        {
            json = JsonSerializer.Deserialize<ComfyWebSocketResponse>(text);
        }
        catch (JsonException e)
        {
            Logger.Warn($"Failed to parse json {text} ({e}), skipping");
            return;
        }

        if (json is null)
        {
            Logger.Warn($"Could not parse json {text}, skipping");
            return;
        }

        Logger.Trace(
            "Received json message: (Type = {Type}, Data = {Data})",
            json.Type,
            json.Data
        );
        
        if (json.Type == ComfyWebSocketResponseType.Executing)
        {
            var executingData = json.GetDataAsType<ComfyWebSocketExecutingData>();
            if (executingData is null)
            {
                Logger.Warn($"Could not parse executing data {json.Data}, skipping");
                return;
            }
            
            // When Node property is null, it means the prompt has finished executing
            // remove the task from the dictionary and set the result
            if (executingData.Node is null)
            {
                if (PromptTasks.TryRemove(executingData.PromptId, out var task))
                {
                    task.SetResult();
                }
                else
                {
                    Logger.Warn($"Could not find task for prompt {executingData.PromptId}, skipping");
                }
            }
            
            ExecutingUpdateReceived?.Invoke(this, executingData);
        }
        else if (json.Type == ComfyWebSocketResponseType.Status)
        {
            var statusData = json.GetDataAsType<ComfyWebSocketStatusData>();
            if (statusData is null)
            {
                Logger.Warn($"Could not parse status data {json.Data}, skipping");
                return;
            }
            
            StatusUpdateReceived?.Invoke(this, statusData);
        }
        else if (json.Type == ComfyWebSocketResponseType.Progress)
        {
            var progressData = json.GetDataAsType<ComfyWebSocketProgressData>();
            if (progressData is null)
            {
                Logger.Warn($"Could not parse progress data {json.Data}, skipping");
                return;
            }

            ProgressUpdateReceived?.Invoke(this, progressData);
        }
        else
        {
            Logger.Warn($"Unknown message type {json.Type} ({json.Data}), skipping");
        }
    }

    /// <summary>
    /// Parses binary data (previews) into image streams
    /// https://github.com/comfyanonymous/ComfyUI/blob/master/server.py#L518
    /// </summary>
    private void HandleBinaryMessage(byte[] data)
    {
        if (data is not {Length: > 4})
        {
            Logger.Warn("The input data is null or not long enough.");
            return;
        }
        
        // The first 4 bytes is int32 of the message type
        // Subsequent 4 bytes following is int32 of the image format
        // The rest is the image data
        
        // Read the image type from the first 4 bytes of the data.
        // Python's struct.pack(">I", type_num) will pack the data as a big-endian unsigned int
        /*var typeBytes = new byte[4];
        stream.Read(typeBytes, 0, 4);
        var imageType = BitConverter.ToInt32(typeBytes, 0);*/

        /*if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(typeBytes);
        }*/
        
        PreviewImageReceived?.Invoke(this, new ComfyWebSocketImageData
        {
            ImageBytes = data[8..],
        });
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await webSocketClient.StartOrFail().ConfigureAwait(false);
    }

    public override async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await webSocketClient
            .StopOrFail(WebSocketCloseStatus.NormalClosure, string.Empty)
            .ConfigureAwait(false);
    }

    public async Task<(ComfyPromptResponse, Task)> QueuePromptAsync(
        Dictionary<string, ComfyNode> nodes,
        CancellationToken cancellationToken = default
    )
    {
        var request = new ComfyPromptRequest { ClientId = ClientId, Prompt = nodes };
        var result = await comfyApi.PostPrompt(request, cancellationToken).ConfigureAwait(false);
        
        // Add task to dictionary
        var tcs = new TaskCompletionSource();
        PromptTasks[result.PromptId] = tcs;

        return (result, tcs.Task);
    }

    public async Task<Dictionary<string, List<ComfyImage>?>> GetImagesForExecutedPromptAsync(
        string promptId, CancellationToken cancellationToken = default)
    {
        // Get history for images
        var history = await comfyApi.GetHistory(promptId, cancellationToken).ConfigureAwait(false);
        
        // Get the current prompt history
        var current = history[promptId];

        var dict = new Dictionary<string, List<ComfyImage>?>();
        foreach (var (nodeKey, output) in current.Outputs)
        {
            dict[nodeKey] = output.Images;
        }
        return dict;
    }
    
    public async Task<Stream> GetImageStreamAsync(ComfyImage comfyImage, CancellationToken cancellationToken = default)
    {
        var response = await comfyApi.GetImage(comfyImage.FileName, comfyImage.SubFolder, comfyImage.Type, cancellationToken).ConfigureAwait(false);
        return response;
    }
    
    /// <summary>
    /// Get a list of strings representing available model names
    /// </summary>
    public async Task<List<string>?> GetModelNamesAsync(CancellationToken cancellationToken = default)
    {
        const string modelLoaderType = "CheckpointLoaderSimple";
        var response = await comfyApi.GetObjectInfo(modelLoaderType, cancellationToken).ConfigureAwait(false);

        var info = response[modelLoaderType];
        return info.Input.GetRequiredValueAsNestedList("ckpt_name");
    }
    
    /// <summary>
    /// Get a list of strings representing available sampler names
    /// </summary>
    public async Task<List<string>?> GetSamplerNamesAsync(CancellationToken cancellationToken = default)
    {
        const string samplerType = "KSampler";
        var response = await comfyApi.GetObjectInfo(samplerType, cancellationToken).ConfigureAwait(false);

        var info = response[samplerType];
        return info.Input.GetRequiredValueAsNestedList("sampler_name");
    }

    protected override void Dispose(bool disposing)
    {
        if (isDisposed) return;
        webSocketClient.Dispose();
        isDisposed = true;
    }
}
