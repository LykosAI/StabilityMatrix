using System;

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
    public float Percentage => (float) Math.Ceiling(Math.Clamp(Progress ?? 0, 0, 1) * 100);
    public ProgressType Type { get; init; } = ProgressType.Generic;
    
    public ProgressReport(double progress, string? title = null, string? message = null, bool isIndeterminate = false, ProgressType type = ProgressType.Generic)
    {
        Progress = progress;
        Title = title;
        Message = message;
        IsIndeterminate = isIndeterminate;
        Type = type;
    }
    
    public ProgressReport(ulong current, ulong total, string? title = null, string? message = null, bool isIndeterminate = false, ProgressType type = ProgressType.Generic)
    {
        Current = current;
        Total = total;
        Progress = (double) current / total;
        Title = title;
        Message = message;
        IsIndeterminate = isIndeterminate;
        Type = type;
    }
    
    public ProgressReport(ulong current, string? title = null, string? message = null, ProgressType type = ProgressType.Generic)
    {
        Current = current;
        Title = title;
        Message = message;
        IsIndeterminate = true;
        Type = type;
    }
}
