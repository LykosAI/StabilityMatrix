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
}
