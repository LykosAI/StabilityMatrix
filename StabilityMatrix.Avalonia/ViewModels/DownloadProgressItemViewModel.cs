using System;
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

        Id = download.Id;
        Name = download.FileName;
        State = download.ProgressState;
        OnProgressStateChanged(State);
        
        // If initial progress provided, load it
        if (download is {TotalBytes: > 0, DownloadedBytes: > 0})
        {
            var current = download.DownloadedBytes / (double) download.TotalBytes;
            Progress.Value = (float) Math.Ceiling(Math.Clamp(current, 0, 1) * 100);
        }
        
        download.ProgressUpdate += (s, e) =>
        {
            Progress.Value = e.Percentage;
            Progress.IsIndeterminate = e.IsIndeterminate;
            Progress.Text = e.Title;
        };
        
        download.ProgressStateChanged += (s, e) =>
        {
            State = e;
            OnProgressStateChanged(e);
        };
    }

    private void OnProgressStateChanged(ProgressState state)
    {
        if (state == ProgressState.Inactive)
        {
            Progress.Text = "Paused";
        }
        else if (state == ProgressState.Working)
        {
            Progress.Text = "Downloading...";
        }
        else if (state == ProgressState.Success)
        {
            Progress.Text = "Completed";
        }
        else if (state == ProgressState.Cancelled)
        {
            Progress.Text = "Cancelled";
        }
        else if (state == ProgressState.Failed)
        {
            Progress.Text = "Failed";
        }
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
