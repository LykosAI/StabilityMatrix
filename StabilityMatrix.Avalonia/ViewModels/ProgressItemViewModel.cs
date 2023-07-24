using System;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class ProgressItemViewModel : ViewModelBase
{
    [ObservableProperty] private Guid id;
    [ObservableProperty] private string name;
    [ObservableProperty] private ProgressReport progress;
    [ObservableProperty] private bool failed;
    [ObservableProperty] private string? progressText;

    public ProgressItemViewModel(ProgressItem progressItem)
    {
        Id = progressItem.ProgressId;
        Name = progressItem.Name;
        Progress = progressItem.Progress;
        Failed = progressItem.Failed;
        ProgressText = GetProgressText(Progress);
        
        EventManager.Instance.ProgressChanged += OnProgressChanged;
    }

    private void OnProgressChanged(object? sender, ProgressItem e)
    {
        if (e.ProgressId != Id)
            return;
        
        Progress = e.Progress;
        Failed = e.Failed;
        ProgressText = GetProgressText(Progress);
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
