using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<LayeredMaskEditorDialog>]
public partial class LayeredMaskEditorDialog : UserControlBase
{
    private ListBox? layerListBox;
    private ScrollViewer? layerScrollViewer;
    private DispatcherTimer? autoScrollTimer;
    private double autoScrollSpeed;
    private const double AutoScrollEdgeThreshold = 50; // Pixels from edge to trigger auto-scroll
    private const double AutoScrollBaseSpeed = 5; // Base scroll speed in pixels per tick
    private bool isDragging;

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

            // Find the parent ScrollViewer
            layerScrollViewer = layerListBox.FindAncestorOfType<ScrollViewer>();
        }

        // Set up auto-scroll timer
        autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
        autoScrollTimer.Tick += AutoScrollTimer_Tick;

        // Subscribe to pointer events for drag detection
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);

        // Subscribe to keyboard events for shortcuts
        // Use Tunnel strategy to intercept before ListBox handles navigation keys
        // Also use handledEventsToo to receive events even if child controls handle them
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
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

        // Clean up timer
        autoScrollTimer?.Stop();
        autoScrollTimer = null;

        RemoveHandler(PointerMovedEvent, OnPointerMoved);
        RemoveHandler(PointerReleasedEvent, OnPointerReleased);
        RemoveHandler(PointerCaptureLostEvent, OnPointerCaptureLost);
        RemoveHandler(KeyDownEvent, OnKeyDown);
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

    private DateTime _lastShortcutTime = DateTime.MinValue;
    private static readonly TimeSpan ShortcutThrottle = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Handles keyboard shortcuts for layer operations.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not LayeredMaskEditorViewModel vm)
            return;

        // Don't handle shortcuts when typing in a TextBox
        if (e.Source is TextBox)
            return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Throttle rapid-fire events for navigation keys
        if ((e.Key == Key.Up || e.Key == Key.Down) && DateTime.UtcNow - _lastShortcutTime < ShortcutThrottle)
        {
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            // Delete - Delete selected layer
            case Key.Delete when !ctrl && !shift:
                if (vm.DeleteLayerCommand.CanExecute(null))
                {
                    vm.DeleteLayerCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            // Ctrl+Shift+Delete - Clear all layers
            case Key.Delete when ctrl && shift:
                vm.ClearAllLayersCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+D - Duplicate layer
            case Key.D when ctrl:
                vm.DuplicateLayerCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+N - New layer
            case Key.N when ctrl:
                vm.AddLayerCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+F - Fill layer
            case Key.F when ctrl:
                vm.FillLayerCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+I - Invert layer
            case Key.I when ctrl:
                vm.InvertLayerCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+Shift+Z - Undo layer operation
            case Key.Z when ctrl && shift:
                if (vm.UndoLayerOperationCommand.CanExecute(null))
                {
                    vm.UndoLayerOperationCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            // Ctrl+Up - Move layer up
            case Key.Up when ctrl:
                if (vm.MoveLayerUpCommand.CanExecute(null))
                {
                    vm.MoveLayerUpCommand.Execute(null);
                    _lastShortcutTime = DateTime.UtcNow;
                    e.Handled = true;
                    FocusSelectedLayer();
                }
                break;

            // Ctrl+Down - Move layer down
            case Key.Down when ctrl:
                if (vm.MoveLayerDownCommand.CanExecute(null))
                {
                    vm.MoveLayerDownCommand.Execute(null);
                    _lastShortcutTime = DateTime.UtcNow;
                    e.Handled = true;
                    FocusSelectedLayer();
                }
                break;
        }
    }

    /// <summary>
    /// Handles pointer move to detect dragging and trigger auto-scroll near edges.
    /// </summary>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (layerScrollViewer == null || layerListBox == null)
            return;

        // Check if we're likely dragging (pointer is captured)
        var pointer = e.Pointer;
        if (pointer.Captured == null)
        {
            StopAutoScroll();
            return;
        }

        // Check if pointer is over/near the layer list area
        var scrollViewerBounds = layerScrollViewer.Bounds;
        var pointerPos = e.GetPosition(layerScrollViewer);

        // Only process if pointer is within the horizontal bounds of the ScrollViewer
        if (pointerPos.X < 0 || pointerPos.X > scrollViewerBounds.Width)
        {
            StopAutoScroll();
            return;
        }

        isDragging = true;

        // Check if near top edge
        if (pointerPos.Y < AutoScrollEdgeThreshold && pointerPos.Y >= -AutoScrollEdgeThreshold)
        {
            // Calculate speed based on proximity to edge (closer = faster)
            var proximity = 1 - (pointerPos.Y / AutoScrollEdgeThreshold);
            autoScrollSpeed = -AutoScrollBaseSpeed * Math.Max(1, proximity * 3);
            StartAutoScroll();
        }
        // Check if near bottom edge
        else if (
            pointerPos.Y > scrollViewerBounds.Height - AutoScrollEdgeThreshold
            && pointerPos.Y <= scrollViewerBounds.Height + AutoScrollEdgeThreshold
        )
        {
            var distanceFromBottom = scrollViewerBounds.Height - pointerPos.Y;
            var proximity = 1 - (distanceFromBottom / AutoScrollEdgeThreshold);
            autoScrollSpeed = AutoScrollBaseSpeed * Math.Max(1, proximity * 3);
            StartAutoScroll();
        }
        else
        {
            StopAutoScroll();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        isDragging = false;
        StopAutoScroll();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        isDragging = false;
        StopAutoScroll();
    }

    private void StartAutoScroll()
    {
        if (autoScrollTimer != null && !autoScrollTimer.IsEnabled)
        {
            autoScrollTimer.Start();
        }
    }

    private void StopAutoScroll()
    {
        autoScrollTimer?.Stop();
        autoScrollSpeed = 0;
    }

    private void AutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (layerScrollViewer == null || !isDragging || autoScrollSpeed == 0)
        {
            StopAutoScroll();
            return;
        }

        var newOffset = layerScrollViewer.Offset.Y + autoScrollSpeed;
        newOffset = Math.Max(0, Math.Min(newOffset, layerScrollViewer.ScrollBarMaximum.Y));
        layerScrollViewer.Offset = new Vector(layerScrollViewer.Offset.X, newOffset);
    }

    private void FocusSelectedLayer()
    {
        if (
            DataContext is not LayeredMaskEditorViewModel vm
            || vm.SelectedLayer == null
            || layerListBox == null
        )
            return;

        var layer = vm.SelectedLayer;
        layerListBox.ScrollIntoView(layer);

        // Post to UI thread to allow layout updates to happen first
        Dispatcher.UIThread.Post(
            () =>
            {
                var container = layerListBox.ContainerFromItem(layer);
                if (container is Control control)
                {
                    control.Focus();
                }
            },
            DispatcherPriority.Input
        );
    }
}
