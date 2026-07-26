namespace StabilityMatrix.Avalonia.Models.Inference;

public enum PromptQueueItemStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
}
