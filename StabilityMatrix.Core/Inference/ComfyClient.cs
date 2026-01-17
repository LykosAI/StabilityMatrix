using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Text.Json;
using NLog;
using Polly.Contrib.WaitAndRetry;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Converters.Json;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;
using StabilityMatrix.Core.Models.FileInterfaces;
using Websocket.Client;
using Websocket.Client.Exceptions;
using Yoh.Text.Json.NamingPolicies;

namespace StabilityMatrix.Core.Inference;

public class ComfyClient : InferenceClientBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly WebsocketClient webSocketClient;
    private readonly IComfyApi comfyApi;
    private bool isDisposed;

    private readonly JsonSerializerOptions jsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicies.SnakeCaseLower,
            Converters =
            {
                new NodeConnectionBaseJsonConverter(),
                new OneOfJsonConverter<string, StringNodeConnection>()
            }
        };

    // ReSharper disable once MemberCanBePrivate.Global
    public string ClientId { get; } = Guid.NewGuid().ToString();

    public Uri BaseAddress { get; }

    /// <summary>
    /// If available, the local path to the server root directory.
    /// </summary>
    public DirectoryPath? LocalServerPath { get; set; }

    /// <summary>
    /// If available, the local server package pair
    /// </summary>
    public PackagePair? LocalServerPackage { get; set; }

    /// <summary>
    /// Path to the "output" folder from LocalServerPath
    /// </summary>
    public DirectoryPath? OutputImagesDir => LocalServerPath?.JoinDir("output");

    /// <summary>
    /// Path to the "input" folder from LocalServerPath
    /// </summary>
    public DirectoryPath? InputImagesDir => LocalServerPath?.JoinDir("input");

    /// <summary>
    /// Dictionary of ongoing prompt execution tasks
    /// </summary>
    public ConcurrentDictionary<string, ComfyTask> PromptTasks { get; } = new();

    /// <summary>
    /// Current running prompt task
    /// </summary>
    private ComfyTask? currentPromptTask;

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
        comfyApi = apiFactory.CreateRefitClient<IComfyApi>(
            baseAddress,
            new RefitSettings
            {
                ContentSerializer = new SystemTextJsonContentSerializer(jsonSerializerOptions),
            }
        );
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
            Name = nameof(ComfyClient),
            ReconnectTimeout = TimeSpan.FromSeconds(30)
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
            json = JsonSerializer.Deserialize<ComfyWebSocketResponse>(text, jsonSerializerOptions);
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

        Logger.Trace("Received json message: (Type = {Type}, Data = {Data})", json.Type, json.Data);

        if (json.Type == ComfyWebSocketResponseType.Executing)
        {
            var executingData = json.GetDataAsType<ComfyWebSocketExecutingData>(jsonSerializerOptions);
            if (executingData?.PromptId is null)
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
                    task.RunningNode = null;
                    task.SetResult();
                    currentPromptTask = null;
                }
                else
                {
                    Logger.Warn($"Could not find task for prompt {executingData.PromptId}, skipping");
                }
            }
            // Otherwise set the task's active node to the one received
            else
            {
                if (PromptTasks.TryGetValue(executingData.PromptId, out var task))
                {
                    task.RunningNode = executingData.Node;
                }
            }

            ExecutingUpdateReceived?.Invoke(this, executingData);
        }
        else if (json.Type == ComfyWebSocketResponseType.Status)
        {
            var statusData = json.GetDataAsType<ComfyWebSocketStatusData>(jsonSerializerOptions);
            if (statusData is null)
            {
                Logger.Warn($"Could not parse status data {json.Data}, skipping");
                return;
            }

            StatusUpdateReceived?.Invoke(this, statusData);
        }
        else if (json.Type == ComfyWebSocketResponseType.Progress)
        {
            var progressData = json.GetDataAsType<ComfyWebSocketProgressData>(jsonSerializerOptions);
            if (progressData is null)
            {
                Logger.Warn($"Could not parse progress data {json.Data}, skipping");
                return;
            }

            // Set for the current prompt task
            currentPromptTask?.OnProgressUpdate(progressData);

            ProgressUpdateReceived?.Invoke(this, progressData);
        }
        else if (json.Type == ComfyWebSocketResponseType.ExecutionError)
        {
            if (
                json.GetDataAsType<ComfyWebSocketExecutionErrorData>(jsonSerializerOptions)
                is not { } errorData
            )
            {
                Logger.Warn($"Could not parse ExecutionError data {json.Data}, skipping");
                return;
            }

            // Set error status
            if (PromptTasks.TryRemove(errorData.PromptId, out var task))
            {
                task.RunningNode = null;
                task.SetException(
                    new ComfyNodeException { ErrorData = errorData, JsonData = json.Data.ToString() }
                );
                currentPromptTask = null;
            }
            else
            {
                Logger.Warn($"Could not find task for prompt {errorData.PromptId}, skipping");
            }
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
        if (data is not { Length: > 4 })
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

        PreviewImageReceived?.Invoke(this, new ComfyWebSocketImageData { ImageBytes = data[8..], });
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var delays = Backoff
            .DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(500), retryCount: 5)
            .ToImmutableArray();

        foreach (var (i, retryDelay) in delays.Enumerate())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await webSocketClient.StartOrFail().ConfigureAwait(false);
                return;
            }
            catch (WebsocketException e)
            {
                Logger.Info(
                    "Failed to connect to websocket, retrying in {RetryDelay} ({Message})",
                    retryDelay,
                    e.Message
                );

                if (i == delays.Length - 1)
                {
                    throw;
                }
            }
        }
    }

    public override async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await webSocketClient.Stop(WebSocketCloseStatus.NormalClosure, string.Empty).ConfigureAwait(false);
    }

public async Task<ComfyTask> QueuePromptAsync(
    Dictionary<string, ComfyNode> nodes,
    CancellationToken cancellationToken = default
)
{
    var request = new ComfyPromptRequest { ClientId = ClientId, Prompt = nodes };

    // DEBUG: dump final workflow JSON
    try
    {
        var json = JsonSerializer.Serialize(request, jsonSerializerOptions);

        var debugDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StabilityMatrix",
            "Debug"
        );

        Directory.CreateDirectory(debugDir);

        var path = Path.Combine(
            debugDir,
            $"wan_workflow_debug_request.json"
        );

        File.WriteAllText(path, json);

        Logger.Warn("WAN DEBUG: Dumped final request JSON to {0}", path);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "WAN DEBUG: Failed to dump final request JSON");
    }

    var result = await comfyApi.PostPrompt(request, cancellationToken).ConfigureAwait(false);

    var task = new ComfyTask(result.PromptId);
    PromptTasks.TryAdd(result.PromptId, task);
    currentPromptTask = task;

    return task;
}

    public async Task InterruptPromptAsync(CancellationToken cancellationToken = default)
    {
        await comfyApi.PostInterrupt(cancellationToken).ConfigureAwait(false);

        // Set the current task to null, and remove it from the dictionary
        if (currentPromptTask is { } task)
        {
            PromptTasks.TryRemove(task.Id, out _);
            task.TrySetCanceled(cancellationToken);
            task.Dispose();
            currentPromptTask = null;
        }
    }

    // Upload images
    public Task<ComfyUploadImageResponse> UploadImageAsync(
        Stream image,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        var streamPart = new StreamPart(image, fileName);
        return comfyApi.PostUploadImage(streamPart, "true", "input", "Inference", cancellationToken);
    }

    /// <summary>
    /// Upload a file to the server at the given relative path from server's root
    /// </summary>
    public async Task UploadFileAsync(
        string sourcePath,
        string destinationRelativePath,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Currently there is no api, so we do a local file copy
        if (LocalServerPath is null)
        {
            throw new InvalidOperationException("LocalServerPath is not set");
        }

        var sourceFile = new FilePath(sourcePath);
        var destFile = LocalServerPath.JoinFile(destinationRelativePath);

        Logger.Info("Copying file from {Source} to {Dest}", sourcePath, destFile);

        if (!sourceFile.Exists)
        {
            throw new FileNotFoundException("Source file does not exist", sourcePath);
        }

        destFile.Directory?.Create();

        await sourceFile.CopyToAsync(destFile, true).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, List<ComfyImage>?>> GetImagesForExecutedPromptAsync(
        string promptId,
        CancellationToken cancellationToken = default
    )
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

    public async Task<Stream> GetImageStreamAsync(
        ComfyImage comfyImage,
        CancellationToken cancellationToken = default
    )
    {
        var response = await comfyApi
            .GetImage(comfyImage.FileName, comfyImage.SubFolder, comfyImage.Type, cancellationToken)
            .ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Get a list of strings representing available model names
    /// </summary>
    public Task<List<string>?> GetModelNamesAsync(CancellationToken cancellationToken = default)
    {
        return GetNodeOptionNamesAsync("CheckpointLoaderSimple", "ckpt_name", cancellationToken);
    }

    /// <summary>
    /// Get a list of strings representing available sampler names
    /// </summary>
    public Task<List<string>?> GetSamplerNamesAsync(CancellationToken cancellationToken = default)
    {
        return GetNodeOptionNamesAsync("KSampler", "sampler_name", cancellationToken);
    }

    /// <summary>
    /// Get a list of strings representing available options of a given node
    /// </summary>
    public async Task<List<string>?> GetNodeOptionNamesAsync(
        string nodeName,
        string optionName,
        CancellationToken cancellationToken = default
    )
    {
        var response = await comfyApi.GetObjectInfo(nodeName, cancellationToken).ConfigureAwait(false);

        var info = response[nodeName];
        return info.Input.GetRequiredValueAsNestedList(optionName);
    }

    /// <summary>
    /// Get a list of strings representing available optional options of a given node
    /// </summary>
    public async Task<List<string>?> GetOptionalNodeOptionNamesAsync(
        string nodeName,
        string optionName,
        CancellationToken cancellationToken = default
    )
    {
        var response = await comfyApi.GetObjectInfo(nodeName, cancellationToken).ConfigureAwait(false);

        var info = response.GetValueOrDefault(nodeName);

        return info?.Input.GetOptionalValueAsNestedList(optionName);
    }

    public async Task<List<string>?> GetRequiredNodeOptionNamesFromOptionalNodeAsync(
        string nodeName,
        string optionName,
        CancellationToken cancellationToken = default
    )
    {
        var response = await comfyApi.GetObjectInfo(nodeName, cancellationToken).ConfigureAwait(false);

        var info = response.GetValueOrDefault(nodeName);

        return info?.Input.GetRequiredValueAsNestedList(optionName);
    }

    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
            return;
        webSocketClient.Dispose();
        isDisposed = true;
    }
}
