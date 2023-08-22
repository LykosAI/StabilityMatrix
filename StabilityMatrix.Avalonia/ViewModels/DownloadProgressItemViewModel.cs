using System.Threading.Tasks;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels;

public class DownloadProgressItemViewModel : PausableProgressItemViewModelBase
{
    private readonly TrackedDownload download;
    
    public DownloadProgressItemViewModel(TrackedDownload download)
    {
        this.download = download;

        Name = download.FileName;
        State = download.ProgressState;
        
        download.ProgressUpdate += (s, e) =>
        {
            Progress.Value = e.Percentage;
            Progress.IsIndeterminate = e.IsIndeterminate;
            Progress.Text = e.Title;
        };
        
        download.ProgressStateChanged += (s, e) =>
        {
            State = e;
            
            if (e == ProgressState.Inactive)
            {
                Progress.Text = "Paused";
            }
            else if (e == ProgressState.Working)
            {
                Progress.Text = "Downloading...";
            }
            else if (e == ProgressState.Success)
            {
                Progress.Text = "Completed";
            }
            else if (e == ProgressState.Cancelled)
            {
                Progress.Text = "Cancelled";
            }
            else if (e == ProgressState.Failed)
            {
                Progress.Text = "Failed";
            }
        };
    }

    /// <inheritdoc />
    public override Task Cancel()
    {
        download.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task Pause()
    {
        download.Pause();
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public override Task Resume()
    {
        download.Resume();
        return Task.CompletedTask;
    }
}
