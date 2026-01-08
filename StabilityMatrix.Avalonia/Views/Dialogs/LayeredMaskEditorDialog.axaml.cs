using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<LayeredMaskEditorDialog>]
public partial class LayeredMaskEditorDialog : UserControlBase
{
    private ListBox? layerListBox;

    public LayeredMaskEditorDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Find the ListBox and subscribe to child index changes for drag reordering
        layerListBox = this.FindControl<ListBox>("LayerItemsControl");
        if (layerListBox != null)
        {
            ((IChildIndexProvider)layerListBox).ChildIndexChanged += OnChildIndexChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        // Unsubscribe from events
        if (layerListBox != null)
        {
            ((IChildIndexProvider)layerListBox).ChildIndexChanged -= OnChildIndexChanged;
        }
    }

    /// <summary>
    /// Handles the child index changed event from the ListBox.
    /// This is fired when a drag reorder operation completes.
    /// </summary>
    private void OnChildIndexChanged(object? sender, ChildIndexChangedEventArgs e)
    {
        if (
            e.Child is Control { DataContext: MaskLayer layer }
            && DataContext is LayeredMaskEditorViewModel vm
        )
        {
            vm.OnLayerIndexChanged(layer, e.Index);
        }
    }
}
