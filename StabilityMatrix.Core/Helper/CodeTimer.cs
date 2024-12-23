using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;

namespace StabilityMatrix.Core.Helper;

[Localizable(false)]
public class CodeTimer(string postFix = "", [CallerMemberName] string callerName = "") : IDisposable
{
    private static readonly Stack<CodeTimer> RunningTimers = new();

    private readonly string name = $"{callerName}" + (string.IsNullOrEmpty(postFix) ? "" : $" ({postFix})");
    private readonly Stopwatch stopwatch = new();
    private bool isDisposed;

    private CodeTimer? ParentTimer { get; set; }
    private List<CodeTimer> SubTimers { get; } = new();

    public void Start()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (stopwatch.IsRunning)
        {
            return;
        }

        stopwatch.Start();

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
    /// Start a new timer and return it.
    /// </summary>
    /// <param name="postFix"></param>
    /// <param name="callerName"></param>
    /// <returns></returns>
    public static CodeTimer StartNew(string postFix = "", [CallerMemberName] string callerName = "")
    {
        var timer = new CodeTimer(postFix, callerName);
        timer.Start();
        return timer;
    }

    /// <summary>
    /// Starts a new timer and returns it if DEBUG is defined, otherwise returns an empty IDisposable
    /// </summary>
    /// <param name="postFix"></param>
    /// <param name="callerName"></param>
    /// <returns></returns>
    public static IDisposable StartDebug(string postFix = "", [CallerMemberName] string callerName = "")
    {
#if DEBUG
        return StartNew(postFix, callerName);
#else
        return Disposable.Empty;
#endif
    }

    /// <summary>
    /// Formats a TimeSpan into a string. Chooses the most appropriate unit of time.
    /// </summary>
    public static string FormatTime(TimeSpan duration)
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
        Debug.Write(message);
    }

    /// <summary>
    /// Get results for this timer and all sub timers recursively
    /// </summary>
    private string GetResult()
    {
        var builder = new StringBuilder();

        builder.AppendLine($"{name}:\ttook {FormatTime(stopwatch.Elapsed)}");

        foreach (var timer in SubTimers)
        {
            // For each sub timer layer, add a `|-` prefix
            builder.AppendLine($"|- {timer.GetResult()}");
        }

        return builder.ToString();
    }

    public void Stop()
    {
        // Output if we're a root timer
        Stop(printOutput: ParentTimer is null);
    }

    public void Stop(bool printOutput)
    {
        if (isDisposed || !stopwatch.IsRunning)
        {
            return;
        }

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
        if (printOutput)
        {
#if DEBUG
            OutputDebug(GetResult());
#else
            Console.WriteLine(GetResult());
#endif
            SubTimers.Clear();
        }
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        Stop();

        isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
