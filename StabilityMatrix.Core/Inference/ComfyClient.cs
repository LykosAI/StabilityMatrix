using System.Net.WebSockets;
using NLog;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Inference;

public class ComfyClient : InferenceClientBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly ComfyWebSocketClient webSocketClient = new();
    private readonly IComfyApi comfyApi;
    private readonly Uri baseAddress;
    private bool isDisposed;

    // ReSharper disable once MemberCanBePrivate.Global
    public string ClientId { get; private set; } = Guid.NewGuid().ToString();
    
    public ComfyClient(IApiFactory apiFactory, Uri baseAddress)
    {
        comfyApi = apiFactory.CreateRefitClient<IComfyApi>(baseAddress);
        this.baseAddress = baseAddress;
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await webSocketClient.ConnectAsync(baseAddress, ClientId).ConfigureAwait(false);
    }

    public override async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await webSocketClient.CloseAsync().ConfigureAwait(false);
    }
    
    public async Task<ComfyPromptResponse> QueuePromptAsync(
        Dictionary<string, ComfyNode> nodes, 
        CancellationToken cancellationToken = default)
    {
        var request = new ComfyPromptRequest
        {
            ClientId = ClientId,
            Prompt = nodes,
        };
        var result = await comfyApi.PostPrompt(request, cancellationToken).ConfigureAwait(false);
        return result;
    }
    
    public async Task<Dictionary<string, List<ComfyImage>?>> ExecutePromptAsync(
        Dictionary<string, ComfyNode> nodes, 
        IProgress<ProgressReport>? progress = default,
        CancellationToken cancellationToken = default)
    {
        var response = await QueuePromptAsync(nodes, cancellationToken).ConfigureAwait(false);
        var promptId = response.PromptId;

        while (true)
        {
            var message = await webSocketClient.ReceiveAsync().ConfigureAwait(false);

            if (message is null)
            {
                Logger.Warn("Received null message");
                break;
            }
            
            // Stop if closed
            if (message.MessageType == WebSocketMessageType.Close)
            {
                Logger.Trace("Received close message");
                break;
            }
            
            if (message.Json is { } json)
            {
                Logger.Trace("Received json message: (Type = {Type}, Data = {Data})", 
                    json.Type, json.Data);
                
                // Stop if we get an executing response with null Node property
                if (json.Type is ComfyWebSocketResponseType.Executing)
                {
                    var executingData = json.GetDataAsType<ComfyWebSocketExecutingData>();
                    // We need this to stop the loop, so if it's null, we'll throw
                    if (executingData is null)
                    {
                        throw new NullReferenceException("Could not parse executing data");
                    }
                    // Check this is for us
                    if (executingData.PromptId != promptId)
                    {
                        Logger.Trace("Received executing message for different prompt - ignoring");
                        continue;
                    }
                    if (executingData.Node is null)
                    {
                        Logger.Trace("Received executing message with null node - stopping");
                        break;
                    }
                }
                else if (json.Type is ComfyWebSocketResponseType.Progress)
                {
                    var progressData = json.GetDataAsType<ComfyWebSocketProgressData>();
                    if (progressData is null)
                    {
                        Logger.Warn("Could not parse progress data");
                        continue;
                    }
                    progress?.Report(new ProgressReport
                    {
                        Current = Convert.ToUInt64(progressData.Value),
                        Total = Convert.ToUInt64(progressData.Max),
                    });
                }
            }
        }
        
        // Get history for images
        var history = await comfyApi.GetHistory(promptId, cancellationToken).ConfigureAwait(false);
        
        var dict = new Dictionary<string, List<ComfyImage>?>();
        foreach (var (nodeKey, output) in history.Outputs)
        {
            dict[nodeKey] = output.Images;
        }
        return dict;
    }

    protected override void Dispose(bool disposing)
    {
        if (isDisposed) return;
        webSocketClient.Dispose();
        isDisposed = true;
    }
}
