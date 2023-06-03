namespace StabilityMatrix.Models;

public record struct ProgressReport
{
    /// <summary>
    /// Progress value as percentage between 0 and 1.
    /// </summary>
    public double? Progress { get; init; } = 0;
    /// <summary>
    /// Current progress count.
    /// </summary>
    public ulong? Current { get; init; } = 0;
    /// <summary>
    /// Total progress count.
    /// </summary>
    public ulong? Total { get; init; } = 0;
    public string? Title { get; init; }
    public string? Message { get; init; }
    public bool IsIndeterminate { get; init; } = false;
    
    public ProgressReport(double progress, string? title = null, string? message = null, bool isIndeterminate = false)
    {
        Progress = progress;
        Title = title;
        Message = message;
        IsIndeterminate = isIndeterminate;
    }
    
    public ProgressReport(ulong current, ulong total, string? title = null, string? message = null, bool isIndeterminate = false)
    {
        Current = current;
        Total = total;
        Progress = (double) current / total;
        Title = title;
        Message = message;
        IsIndeterminate = isIndeterminate;
    }
    
    public ProgressReport(ulong current, string? title = null, string? message = null)
    {
        Current = current;
        Title = title;
        Message = message;
        IsIndeterminate = true;
    }
}
