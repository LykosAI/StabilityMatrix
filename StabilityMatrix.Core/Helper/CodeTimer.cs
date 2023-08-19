using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace StabilityMatrix.Core.Helper;

public class CodeTimer : IDisposable
{
    private static readonly Stack<CodeTimer> RunningTimers = new();
    
    private readonly string name;
    private readonly Stopwatch stopwatch;

    private CodeTimer? ParentTimer { get; }
    private List<CodeTimer> SubTimers { get; } = new();
    
    public CodeTimer([CallerMemberName] string? name = null)
    {
        this.name = name ?? "";
        stopwatch = Stopwatch.StartNew();
        
        // Set parent as the top of the stack
        if (RunningTimers.TryPeek(out var timer))
        {
            ParentTimer = timer;
            timer.SubTimers.Add(this);
        }
        
        // Add ourselves to the stack
        RunningTimers.Push(this);
    }
    
    /// <summary>
    /// Formats a TimeSpan into a string. Chooses the most appropriate unit of time.
    /// </summary>
    private static string FormatTime(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
        {
            return $"{duration.TotalMilliseconds:0.00}ms";
        }

        if (duration.TotalMinutes < 1)
        {
            return $"{duration.TotalSeconds:0.00}s";
        }

        if (duration.TotalHours < 1)
        {
            return $"{duration.TotalMinutes:0.00}m";
        }

        return $"{duration.TotalHours:0.00}h";
    }

    private static void OutputDebug(string message)
    {
        Debug.WriteLine(message);
    }

    /// <summary>
    /// Get results for this timer and all sub timers recursively
    /// </summary>
    private string GetResult()
    {
        var builder = new StringBuilder();
        
        builder.AppendLine($"{name}: took {FormatTime(stopwatch.Elapsed)}");
        
        foreach (var timer in SubTimers)
        {
            // For each sub timer layer, add a `|-` prefix
            builder.AppendLine($"|- {timer.GetResult()}");
        }
        
        return builder.ToString();
    }
    
    public void Dispose()
    {
        stopwatch.Stop();
        
        // Remove ourselves from the stack
        if (RunningTimers.TryPop(out var timer))
        {
            if (timer != this)
            {
                throw new InvalidOperationException("Timer stack is corrupted");
            }
        }
        else
        {
            throw new InvalidOperationException("Timer stack is empty");
        }
        
        // If we're a root timer, output all results
        if (ParentTimer is null)
        {
            OutputDebug(GetResult());
            SubTimers.Clear();
        }
        
        GC.SuppressFinalize(this);
    }
}
