using System.Collections.ObjectModel;
using System.ComponentModel;
using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manages the inference prompt queue: a persisted, reorderable list of generation requests
/// executed sequentially one at a time.
/// </summary>
public interface IPromptQueueService : INotifyPropertyChanged
{
    /// <summary>
    /// The queued items, in execution order. Only mutated on the UI thread.
    /// </summary>
    ObservableCollection<PromptQueueItem> Items { get; }

    /// <summary>
    /// Whether the sequential processor is currently running.
    /// </summary>
    bool IsProcessing { get; }

    /// <summary>
    /// The item currently being executed, if any.
    /// </summary>
    PromptQueueItem? CurrentItem { get; }

    void Enqueue(PromptQueueItem item);

    void Remove(PromptQueueItem item);

    /// <summary>
    /// Resets a finished (completed, failed, or cancelled) item back to pending in place so it
    /// runs again. No-op for pending or running items.
    /// </summary>
    void Requeue(PromptQueueItem item);

    /// <summary>
    /// Re-queues every finished item, preserving queue order.
    /// </summary>
    void RequeueAllFinished();

    void MoveUp(PromptQueueItem item);

    void MoveDown(PromptQueueItem item);

    /// <summary>
    /// Removes all completed, failed, and cancelled items.
    /// </summary>
    void ClearFinished();

    /// <summary>
    /// Removes all pending items (does not affect the running item).
    /// </summary>
    void ClearPending();

    /// <summary>
    /// Starts processing pending items sequentially.
    /// </summary>
    void StartProcessing();

    /// <summary>
    /// Requests a stop: the current item finishes but no further items are started.
    /// </summary>
    void StopProcessing();

    /// <summary>
    /// Cancels the item currently running (if any).
    /// </summary>
    void CancelCurrent();
}
