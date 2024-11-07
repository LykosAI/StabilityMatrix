using System;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Progress;

public class DownloadProgressItemViewModel : PausableProgressItemViewModelBase
{
    private readonly ITrackedDownloadService downloadService;
    private readonly TrackedDownload download;

    public DownloadProgressItemViewModel(ITrackedDownloadService downloadService, TrackedDownload download)
    {
        this.downloadService = downloadService;
        this.download = download;

        Id = download.Id;
        Name = download.FileName;
        State = download.ProgressState;
        OnProgressStateChanged(State);

        // If initial progress provided, load it
        if (download is { TotalBytes: > 0, DownloadedBytes: > 0 })
        {
            var current = download.DownloadedBytes / (double)download.TotalBytes;
            Progress.Value = (float)Math.Ceiling(Math.Clamp(current, 0, 1) * 100);
        }

        download.ProgressUpdate += (s, e) =>
        {
            Progress.Value = e.Percentage;
            Progress.IsIndeterminate = e.IsIndeterminate;
            Progress.DownloadSpeedInMBps = e.SpeedInMBps;
        };

        download.ProgressStateChanged += (s, e) =>
        {
            State = e;
            OnProgressStateChanged(e);
        };
    }

    private void OnProgressStateChanged(ProgressState state)
    {
        if (state is ProgressState.Inactive or ProgressState.Paused)
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
        else if (state == ProgressState.Pending)
        {
            Progress.Text = "Waiting for other downloads to finish";
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
        State = ProgressState.Paused;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task Resume()
    {
        return downloadService.TryResumeDownload(download);
    }
}
