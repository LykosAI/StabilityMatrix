using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels.Progress;

public class ProgressItemViewModel : ProgressItemViewModelBase
{
    public ProgressItemViewModel(ProgressItem progressItem)
    {
        Id = progressItem.ProgressId;
        Name = progressItem.Name;
        Progress.Value = progressItem.Progress.Percentage;
        Failed = progressItem.Failed;
        Progress.Text = GetProgressText(progressItem.Progress);
        Progress.IsIndeterminate = progressItem.Progress.IsIndeterminate;
        
        EventManager.Instance.ProgressChanged += OnProgressChanged;
    }

    private void OnProgressChanged(object? sender, ProgressItem e)
    {
        if (e.ProgressId != Id)
            return;
        
        Progress.Value = e.Progress.Percentage;
        Failed = e.Failed;
        Progress.Text = GetProgressText(e.Progress);
        Progress.IsIndeterminate = e.Progress.IsIndeterminate;
    }

    private string GetProgressText(ProgressReport report)
    {
        switch (report.Type)
        {
            case ProgressType.Generic:
                break;
            case ProgressType.Download:
                return Failed ? "Download Failed" : "Downloading...";
            case ProgressType.Extract:
                return Failed ? "Extraction Failed" : "Extracting...";
            case ProgressType.Update:
                return Failed ? "Update Failed" : "Updating...";
        }

        if (Failed)
        {
            return "Failed";
        }

        return string.IsNullOrWhiteSpace(report.Message)
            ? string.IsNullOrWhiteSpace(report.Title) 
                ? string.Empty 
                : report.Title
            : report.Message;
    }
}
