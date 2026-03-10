using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
public abstract partial class PausableProgressItemViewModelBase : ProgressItemViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsPaused),
        nameof(IsCompleted),
        nameof(CanPauseResume),
        nameof(CanCancel),
        nameof(CanRetry)
    )]
    private ProgressState state = ProgressState.Inactive;

    /// <summary>
    /// Whether the progress is paused
    /// </summary>
    public bool IsPaused => State is ProgressState.Inactive or ProgressState.Paused;
    public bool IsPending => State == ProgressState.Pending;

    /// <summary>
    /// Whether the progress has succeeded, failed or was cancelled
    /// </summary>
    public override bool IsCompleted =>
        State is ProgressState.Success or ProgressState.Failed or ProgressState.Cancelled;

    public virtual bool SupportsPauseResume => true;
    public virtual bool SupportsCancel => true;

    /// <summary>
    /// Override to true in subclasses that support manual retry after failure.
    /// Defaults to false so unrelated progress item types are never affected.
    /// </summary>
    public virtual bool SupportsRetry => false;

    public bool CanPauseResume => SupportsPauseResume && !IsCompleted && !IsPending;
    public bool CanCancel => SupportsCancel && !IsCompleted;

    /// <summary>
    /// True only when this item supports retry AND is in the Failed state.
    /// </summary>
    public bool CanRetry => SupportsRetry && State == ProgressState.Failed;

    private AsyncRelayCommand? pauseCommand;
    public IAsyncRelayCommand PauseCommand => pauseCommand ??= new AsyncRelayCommand(Pause);

    public virtual Task Pause() => Task.CompletedTask;

    private AsyncRelayCommand? resumeCommand;
    public IAsyncRelayCommand ResumeCommand => resumeCommand ??= new AsyncRelayCommand(Resume);

    public virtual Task Resume() => Task.CompletedTask;

    private AsyncRelayCommand? cancelCommand;
    public IAsyncRelayCommand CancelCommand => cancelCommand ??= new AsyncRelayCommand(Cancel);

    public virtual Task Cancel() => Task.CompletedTask;

    private AsyncRelayCommand? retryCommand;
    public IAsyncRelayCommand RetryCommand => retryCommand ??= new AsyncRelayCommand(Retry);

    public virtual Task Retry() => Task.CompletedTask;

    [RelayCommand]
    private Task TogglePauseResume()
    {
        return IsPaused ? Resume() : Pause();
    }
}
