using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace StabilityMatrix.Avalonia.Behaviors;

/// <summary>
/// Keeps a virtualizing <see cref="ItemsRepeater"/> showing the right items after its source
/// changes while the repeater is hidden but still attached, such as in a collapsed dock pane.
/// </summary>
/// <remarks>
/// ItemsRepeater does not lay out while hidden. On its first measure after being shown, items
/// inserted above its realized range are placed above the old anchor, and it waits for the scroll
/// viewer to shift down to them. At the top of the list that shift never comes, so the repeater
/// keeps realizing the old window and the new items stay blank until the user scrolls.
/// Re-applying the layout resets that viewport state.
/// </remarks>
public class ItemsRepeaterHiddenChangesBehavior : Behavior<ItemsRepeater>
{
    private INotifyCollectionChanged? observedSource;
    private bool changedWhileHidden;

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject!.PropertyChanged += OnRepeaterPropertyChanged;
        AssociatedObject.LayoutUpdated += OnRepeaterLayoutUpdated;
        ObserveSource(AssociatedObject.ItemsSource);
    }

    protected override void OnDetaching()
    {
        AssociatedObject!.PropertyChanged -= OnRepeaterPropertyChanged;
        AssociatedObject.LayoutUpdated -= OnRepeaterLayoutUpdated;
        ObserveSource(null);

        base.OnDetaching();
    }

    private void OnRepeaterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ItemsRepeater.ItemsSourceProperty)
        {
            ObserveSource(e.NewValue);
        }
    }

    private void ObserveSource(object? source)
    {
        if (observedSource is not null)
        {
            observedSource.CollectionChanged -= OnSourceCollectionChanged;
        }

        observedSource = source as INotifyCollectionChanged;

        if (observedSource is not null)
        {
            observedSource.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AssociatedObject is { IsEffectivelyVisible: false })
        {
            changedWhileHidden = true;
        }
    }

    private void OnRepeaterLayoutUpdated(object? sender, EventArgs e)
    {
        if (!changedWhileHidden || AssociatedObject is not { IsEffectivelyVisible: true } repeater)
        {
            return;
        }

        changedWhileHidden = false;

        var layout = repeater.Layout;
        repeater.Layout = null;
        repeater.Layout = layout;
    }
}
