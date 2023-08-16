using System;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Base;

public class ViewModelBase : ObservableValidator, IRemovableListItem
{
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

    protected void RemoveFromParentList() => parentListRemoveRequestedEventManager?.RaiseEvent(
        this, EventArgs.Empty, nameof(ParentListRemoveRequested));
    
    public virtual void OnLoaded()
    {
        
    }

    public virtual Task OnLoadedAsync()
    {
        return Task.CompletedTask;
    }
    
    public virtual void OnUnloaded()
    {
        
    }
    
    public virtual Task OnUnloadedAsync()
    {
        return Task.CompletedTask;
    }
}
