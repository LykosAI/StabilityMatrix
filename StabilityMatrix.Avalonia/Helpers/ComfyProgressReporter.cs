using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Forwards a ComfyTask's progress events to an Image Lab generation request as
/// <see cref="ImageGenerationProgress"/> updates, deduplicated by (percent, running node).
/// Reports a "Queued" stage immediately on construction and subscribes to the task's
/// events; dispose to unsubscribe.
/// </summary>
public sealed class ComfyProgressReporter : IDisposable
{
    private readonly ComfyTask task;
    private readonly string providerId;
    private readonly IProgress<ImageGenerationProgress>? progress;

    private int? lastPercent;
    private string? lastRunningNode;

    public ComfyProgressReporter(
        ComfyTask task,
        string providerId,
        IProgress<ImageGenerationProgress>? progress
    )
    {
        this.task = task;
        this.providerId = providerId;
        this.progress = progress;

        progress?.Report(
            new ImageGenerationProgress(
                providerId,
                task.Id,
                Value: null,
                Maximum: null,
                RunningNode: null,
                Stage: "Queued"
            )
        );

        task.ProgressUpdate += OnProgressUpdate;
        task.RunningNodeChanged += OnRunningNodeChanged;
    }

    private void Report(int? value, int? maximum, string? runningNode, string? stage)
    {
        var update = new ImageGenerationProgress(providerId, task.Id, value, maximum, runningNode, stage);

        if (
            update.Percent == lastPercent
            && string.Equals(lastRunningNode, runningNode, StringComparison.Ordinal)
        )
        {
            return;
        }

        lastPercent = update.Percent;
        lastRunningNode = runningNode;

        progress?.Report(update);
    }

    private void OnProgressUpdate(object? sender, ComfyProgressUpdateEventArgs args)
    {
        Report(args.Value, args.Maximum, args.RunningNode, "Generating");
    }

    private void OnRunningNodeChanged(object? sender, string? node)
    {
        Report(task.LastProgressUpdate?.Value, task.LastProgressUpdate?.Maximum, node, "Generating");
    }

    public void Dispose()
    {
        task.ProgressUpdate -= OnProgressUpdate;
        task.RunningNodeChanged -= OnRunningNodeChanged;
    }
}
