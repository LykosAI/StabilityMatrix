namespace StabilityMatrix.Core.Models.Progress;

public enum ProgressState
{
    Inactive,
    Paused,
    Pending,
    Working,
    Success,
    Failed,
    Cancelled
}
