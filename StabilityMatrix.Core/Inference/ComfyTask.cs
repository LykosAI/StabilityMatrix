using System.ComponentModel;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Core.Inference;

public class ComfyTask : TaskCompletionSource, IDisposable
{
    public string Id { get; set; }

    private string? runningNode;
    public string? RunningNode
    {
        get => runningNode;
        set
        {
            runningNode = value;
            RunningNodeChanged?.Invoke(this, value);
        }
    }

    public ComfyProgressUpdateEventArgs? LastProgressUpdate { get; private set; }

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
        var args = new ComfyProgressUpdateEventArgs(update.Value, update.Max, Id, RunningNode);
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
