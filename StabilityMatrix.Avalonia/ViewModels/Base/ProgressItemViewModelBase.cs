using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public abstract partial class ProgressItemViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private Guid id;

    [ObservableProperty]
    private string? name;

    [ObservableProperty]
    private bool failed;

    public virtual bool IsCompleted => Progress.Value >= 100 || Failed;

    public ContentDialogProgressViewModelBase Progress { get; init; } = new();
}
