using System.Threading;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockDownloadProgressItemViewModel : PausableProgressItemViewModelBase
{
    private Task? dummyTask;
    private CancellationTokenSource? cts;

    public MockDownloadProgressItemViewModel(string fileName)
    {
        Name = fileName;
        Progress.Value = 5;
        Progress.IsIndeterminate = false;
        Progress.Text = "Downloading...";
    }
    
    /// <inheritdoc />
    public override Task Cancel()
    {
        // Cancel the task that updates progress
        cts?.Cancel();
        cts = null;
        dummyTask = null;

        State = ProgressState.Cancelled;
        Progress.Text = "Cancelled";
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task Pause()
    {
        // Cancel the task that updates progress
        cts?.Cancel();
        cts = null;
        dummyTask = null;
        
        State = ProgressState.Inactive;
        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public override Task Resume()
    {
        // Start a task that updates progress every 100ms
        cts = new CancellationTokenSource();
        dummyTask = Task.Run(async () =>
        {
            while (State != ProgressState.Success)
            {
                await Task.Delay(100, cts.Token);
                Progress.Value += 1;
            }
        }, cts.Token);
        
        State = ProgressState.Working;
        
        return Task.CompletedTask;
    }
}
