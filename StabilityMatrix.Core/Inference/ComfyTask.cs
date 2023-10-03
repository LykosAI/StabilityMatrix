using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Core.Inference;

public class ComfyTask : TaskCompletionSource, IDisposable
{
    public string Id { get; }

    private string? runningNode;
    public string? RunningNode
    {
        get => runningNode;
        set
        {
            runningNode = value;
            RunningNodeChanged?.Invoke(this, value);
            if (value != null)
            {
                RunningNodesHistory.Push(value);
            }
        }
    }

    public Stack<string> RunningNodesHistory { get; } = new();

    public ComfyProgressUpdateEventArgs? LastProgressUpdate { get; private set; }

    public bool HasProgressUpdateStarted => LastProgressUpdate != null;

    public EventHandler<ComfyProgressUpdateEventArgs>? ProgressUpdate;

    public event EventHandler<string?>? RunningNodeChanged;

    public ComfyTask(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Handler for progress updates
    /// </summary>
    public void OnProgressUpdate(ComfyWebSocketProgressData update)
    {
        RunningNodesHistory.TryPeek(out var lastNode);
        var args = new ComfyProgressUpdateEventArgs(update.Value, update.Max, Id, lastNode);
        ProgressUpdate?.Invoke(this, args);
        LastProgressUpdate = args;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ProgressUpdate = null;
        GC.SuppressFinalize(this);
    }
}
