using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace StabilityMatrix.Helper;

public class AsyncDispatcherTimer : DispatcherTimer
{
    public AsyncDispatcherTimer()
    {
        Tick += AsyncDispatcherTimer_Tick;
    }

    private async void AsyncDispatcherTimer_Tick(object? sender, EventArgs e)
    {
        if (TickTask == null)
        {
            // no task to run
            return;
        }

        if (IsRunning && !IsReentrant)
        {
            // previous task hasn't completed
            return;
        }

        try
        {
            IsRunning = true;
            await TickTask.Invoke();
        }
        catch (Exception)
        {
            Debug.WriteLine("Task Failed");
            throw;
        }
        finally
        {
            // allow it to run again
            IsRunning = false;
        }
    }

    public bool IsReentrant { get; set; }
    public bool IsRunning { get; private set; }

    public Func<Task>? TickTask { get; set; }
}
