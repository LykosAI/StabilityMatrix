using System.Reactive;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Core.Inference;

public class ComfyTask : TaskCompletionSource, IDisposable
{
    public string Id { get; set; }
    
    public string? RunningNode { get; set; }

    public EventHandler<ComfyProgressUpdateEventArgs>? ProgressUpdate;
    
    public ComfyTask(string id)
    {
        Id = id;
    }
    
    /// <summary>
    /// Handler for progress updates
    /// </summary>
    public void OnProgressUpdate(ComfyWebSocketProgressData update)
    {
        ProgressUpdate?.Invoke(this, new ComfyProgressUpdateEventArgs(update.Value, update.Max, Id, RunningNode));
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        ProgressUpdate = null;
        GC.SuppressFinalize(this);
    }
}
