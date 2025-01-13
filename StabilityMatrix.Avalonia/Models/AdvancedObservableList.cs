using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Observable AvaloniaList supporting child item deletion requests.
/// </summary>
public class AdvancedObservableList<T> : AvaloniaList<T>
{
    /// <inheritdoc />
    public AdvancedObservableList()
    {
        CollectionChanged += CollectionChangedEventRegistrationHandler;
    }

    /// <inheritdoc />
    public AdvancedObservableList(IEnumerable<T> items)
        : base(items)
    {
        CollectionChanged += CollectionChangedEventRegistrationHandler;
    }

    private void CollectionChangedEventRegistrationHandler(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                TryUnregisterRemovableListItem((T)item);
            }
        }
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                TryRegisterRemovableListItem((T)item);
            }
        }
    }

    private void OnItemRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is T item)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (item is ViewModelBase vm)
                {
                    vm.OnUnloaded();
                }

                Remove(item);
            });
        }
    }

    private bool TryRegisterRemovableListItem(T item)
    {
        if (item is IRemovableListItem removableListItem)
        {
            removableListItem.ParentListRemoveRequested += OnItemRemoveRequested;
            return true;
        }
        return false;
    }

    private bool TryUnregisterRemovableListItem(T item)
    {
        if (item is IRemovableListItem removableListItem)
        {
            removableListItem.ParentListRemoveRequested -= OnItemRemoveRequested;
            return true;
        }
        return false;
    }
}
