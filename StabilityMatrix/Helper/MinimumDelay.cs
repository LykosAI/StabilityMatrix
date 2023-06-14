using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace StabilityMatrix.Helper;

/// <summary>
/// Enforces a minimum delay if the function returns too quickly.
/// Waits during async Dispose.
/// </summary>
public class MinimumDelay : IAsyncDisposable
{
    private readonly Stopwatch stopwatch = new();
    private readonly TimeSpan delay;
    
    /// <summary>
    /// Minimum random delay in milliseconds.
    /// </summary>
    public MinimumDelay(int randMin, int randMax)
    {
        stopwatch.Start();
        Random rand = new();
        delay = TimeSpan.FromMilliseconds(rand.Next(randMin, randMax));
    }
    
    /// <summary>
    /// Minimum fixed delay in milliseconds.
    /// </summary>
    public MinimumDelay(int delayMilliseconds)
    {
        stopwatch.Start();
        delay = TimeSpan.FromMilliseconds(delayMilliseconds);
    }
    
    public async ValueTask DisposeAsync()
    {
        stopwatch.Stop();
        var elapsed = stopwatch.Elapsed;
        if (elapsed < delay)
        {
            await Task.Delay(delay - elapsed);
        }
        GC.SuppressFinalize(this);
    }
}
