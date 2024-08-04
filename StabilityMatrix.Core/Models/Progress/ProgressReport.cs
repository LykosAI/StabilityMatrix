using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.Progress;

public readonly record struct ProgressReport
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
    public ProcessOutput? ProcessOutput { get; init; }
    public bool IsIndeterminate { get; init; } = false;
    public float Percentage => (float)Math.Ceiling(Math.Clamp(Progress ?? 0, 0, 1) * 100);
    public ProgressType Type { get; init; } = ProgressType.Generic;
    public bool PrintToConsole { get; init; } = true;
    public double SpeedInMBps { get; init; } = 0f;

    public static ProgressReport ForProcessOutput(ProcessOutput output) =>
        new(-1f, isIndeterminate: true) { ProcessOutput = output };

    public ProgressReport(
        double progress,
        string? title = null,
        string? message = null,
        bool isIndeterminate = false,
        bool printToConsole = true,
        double speedInMBps = 0,
        ProgressType type = ProgressType.Generic
    )
    {
        Progress = progress;
        Title = title;
        Message = message;
        IsIndeterminate = isIndeterminate;
        Type = type;
        PrintToConsole = printToConsole;
        SpeedInMBps = speedInMBps;
    }

    public ProgressReport(
        ulong current,
        ulong total,
        string? title = null,
        string? message = null,
        bool isIndeterminate = false,
        bool printToConsole = true,
        double speedInMBps = 0,
        ProgressType type = ProgressType.Generic
    )
    {
        Current = current;
        Total = total;
        Progress = (double)current / total;
        Title = title;
        Message = message;
        IsIndeterminate = isIndeterminate;
        Type = type;
        PrintToConsole = printToConsole;
        SpeedInMBps = speedInMBps;
    }

    public ProgressReport(
        int current,
        int total,
        string? title = null,
        string? message = null,
        bool isIndeterminate = false,
        bool printToConsole = true,
        double speedInMBps = 0,
        ProgressType type = ProgressType.Generic
    )
    {
        if (current < 0)
            throw new ArgumentOutOfRangeException(nameof(current), "Current progress cannot negative.");
        if (total < 0)
            throw new ArgumentOutOfRangeException(nameof(total), "Total progress cannot be negative.");
        Current = (ulong)current;
        Total = (ulong)total;
        Progress = (double)current / total;
        Title = title;
        Message = message;
        IsIndeterminate = isIndeterminate;
        Type = type;
        PrintToConsole = printToConsole;
        SpeedInMBps = speedInMBps;
    }

    public ProgressReport(
        ulong current,
        string? title = null,
        string? message = null,
        ProgressType type = ProgressType.Generic
    )
    {
        Current = current;
        Title = title;
        Message = message;
        IsIndeterminate = true;
        Type = type;
    }

    // Implicit conversion from action
}
