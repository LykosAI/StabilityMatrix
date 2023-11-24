using System;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public partial class ViewModelBase : ObservableValidator, IRemovableListItem
{
    [PublicAPI]
    protected ViewModelState ViewModelState { get; private set; }

    private WeakEventManager? parentListRemoveRequestedEventManager;

    public event EventHandler ParentListRemoveRequested
    {
        add
        {
            parentListRemoveRequestedEventManager ??= new WeakEventManager();
            parentListRemoveRequestedEventManager.AddEventHandler(value);
        }
        remove => parentListRemoveRequestedEventManager?.RemoveEventHandler(value);
    }

    [RelayCommand]
    protected void RemoveFromParentList() =>
        parentListRemoveRequestedEventManager?.RaiseEvent(
            this,
            EventArgs.Empty,
            nameof(ParentListRemoveRequested)
        );

    /// <summary>
    /// Called when the view's LoadedEvent is fired.
    /// </summary>
    public virtual void OnLoaded()
    {
        if (!ViewModelState.HasFlag(ViewModelState.InitialLoaded))
        {
            ViewModelState |= ViewModelState.InitialLoaded;
            OnInitialLoaded();
        }
    }

    /// <summary>
    /// Called the first time the view's LoadedEvent is fired.
    /// Sets the <see cref="ViewModelState.InitialLoaded"/> flag.
    /// </summary>
    protected virtual void OnInitialLoaded() { }

    /// <summary>
    /// Called asynchronously when the view's LoadedEvent is fired.
    /// Runs on the UI thread via Dispatcher.UIThread.InvokeAsync.
    /// The view loading will not wait for this to complete.
    /// </summary>
    public virtual Task OnLoadedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the view's UnloadedEvent is fired.
    /// </summary>
    public virtual void OnUnloaded() { }

    /// <summary>
    /// Called asynchronously when the view's UnloadedEvent is fired.
    /// Runs on the UI thread via Dispatcher.UIThread.InvokeAsync.
    /// The view loading will not wait for this to complete.
    /// </summary>
    public virtual Task OnUnloadedAsync()
    {
        return Task.CompletedTask;
    }
}
