using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Controls;

namespace StabilityMatrix.Avalonia.Controls;

public class PaintCanvas : TemplatedControlBase
{
    private ConcurrentDictionary<long, PenPath> TemporaryPaths => ViewModel!.TemporaryPaths;

    private ImmutableList<PenPath> Paths
    {
        get => ViewModel!.Paths;
        set => ViewModel!.Paths = value;
    }

    private IDisposable? viewModelSubscription;

    private PaintCanvasViewModel? ViewModel { get; set; }

    private SkiaCustomCanvas? MainCanvas { get; set; }

    static PaintCanvas()
    {
        AffectsRender<PaintCanvas>(BoundsProperty);
    }

    public void RefreshCanvas()
    {
        Dispatcher.UIThread.Post(() => MainCanvas?.InvalidateVisual(), DispatcherPriority.Render);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        MainCanvas = e.NameScope.Find<SkiaCustomCanvas>("PART_MainCanvas");

        Debug.Assert(MainCanvas != null);

        if (MainCanvas is not null)
        {
            if (DataContext is PaintCanvasViewModel { CanvasSize: var canvasSize })
            {
                MainCanvas.Width = canvasSize.Width;
                MainCanvas.Height = canvasSize.Height;
            }

            MainCanvas.RenderSkia += OnRenderSkia;
            MainCanvas.PointerEntered += MainCanvas_OnPointerEntered;
            MainCanvas.PointerExited += MainCanvas_OnPointerExited;
        }

        var zoomBorder = e.NameScope.Find<ZoomBorder>("PART_ZoomBorder");
        if (zoomBorder is not null)
        {
            zoomBorder.ZoomChanged += (_, zoomEventArgs) =>
            {
                if (ViewModel is not null)
                {
                    ViewModel.CurrentZoom = zoomEventArgs.ZoomX;

                    UpdateCanvasCursor();
                }
            };

            if (ViewModel is not null)
            {
                ViewModel.CurrentZoom = zoomBorder.ZoomX;

                UpdateCanvasCursor();
            }
        }

        OnDataContextChanged(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is PaintCanvasViewModel viewModel)
        {
            // Set the remote actions
            viewModel.RefreshCanvas = RefreshCanvas;

            viewModelSubscription?.Dispose();
            viewModelSubscription = viewModel
                .WhenPropertyChanged(vm => vm.CanvasSize)
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(change =>
                {
                    if (MainCanvas is not null && !change.Value.IsEmpty)
                    {
                        MainCanvas.Width = change.Value.Width;
                        MainCanvas.Height = change.Value.Height;
                        MainCanvas.InvalidateVisual();
                    }
                });

            ViewModel = viewModel;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEnabledProperty)
        {
            var newIsEnabled = change.GetNewValue<bool>();

            if (!newIsEnabled && ViewModel is { } vm)
            {
                vm.IsPenDown = false;
            }

            // On any enabled change, flush temporary paths
            if (!TemporaryPaths.IsEmpty)
            {
                Paths = Paths.AddRange(TemporaryPaths.Values);
                TemporaryPaths.Clear();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        UpdateMainCanvasBounds();
    }

    private void HandlePointerEvent(PointerEventArgs e)
    {
        // Ignore if disabled
        if (!IsEnabled)
        {
            return;
        }

        if (ViewModel is not { IsDrawingEnabled: true } vm)
        {
            return;
        }

        if (e.RoutedEvent == PointerReleasedEvent && e.Pointer.Type == PointerType.Touch)
        {
            TemporaryPaths.TryRemove(e.Pointer.Id, out _);
            vm.CancelShapeDrawing();
            return;
        }

        e.Handled = true;

        // Must have this or stylus inputs lost after a while
        // https://github.com/AvaloniaUI/Avalonia/issues/12289#issuecomment-1695620412
        e.PreventGestureRecognition();

        var currentPoint = e.GetCurrentPoint(this);

        if (e.RoutedEvent == PointerPressedEvent)
        {
            // Ignore if mouse and not left button
            if (e.Pointer.Type == PointerType.Mouse && !currentPoint.Properties.IsLeftButtonPressed)
            {
                return;
            }

            vm.IsPenDown = true;

            if (vm.SelectedTool == PaintCanvasTool.PaintBucket)
            {
                // Paint bucket: perform flood fill on click
                var position = e.GetPosition(MainCanvas);
                var fillColor = vm.PaintBrushSKColor.WithAlpha((byte)(vm.PaintBrushAlpha * 255));
                vm.FloodFillAt(new SKPoint((float)position.X, (float)position.Y), fillColor);
                vm.IsPenDown = false;
            }
            else if (vm.IsShapeTool)
            {
                var position = e.GetPosition(MainCanvas);
                vm.StartShapeDrawing(new SKPoint((float)position.X, (float)position.Y), e.Pointer.Id);
            }
            else
            {
                HandlePointerMoved(e);
            }
        }
        else if (e.RoutedEvent == PointerReleasedEvent)
        {
            if (vm.IsPenDown)
            {
                if (vm.IsShapeTool && vm.ShapeStartPoint.HasValue)
                {
                    var endPoint = e.GetPosition(MainCanvas);
                    vm.FinalizeShape(new SKPoint((float)endPoint.X, (float)endPoint.Y));
                }
                else
                {
                    HandlePointerMoved(e);
                }

                vm.IsPenDown = false;
            }

            if (!vm.IsShapeTool && TemporaryPaths.TryGetValue(e.Pointer.Id, out var path))
            {
                Paths = Paths.Add(path);
                vm.ClearRedoStack(); // New path added, clear redo history
            }

            if (!vm.IsShapeTool)
            {
                TemporaryPaths.TryRemove(e.Pointer.Id, out _);
            }
        }
        else
        {
            // Moved event
            if (!vm.IsPenDown)
            {
                return;
            }

            if (vm.IsShapeTool && vm.ShapeStartPoint.HasValue)
            {
                var endPoint = e.GetPosition(MainCanvas);
                vm.UpdateShapePreview(new SKPoint((float)endPoint.X, (float)endPoint.Y));
            }
            else if (currentPoint.Properties.Pressure != 0)
            {
                HandlePointerMoved(e);
            }
        }

        Dispatcher.UIThread.Post(() => MainCanvas?.InvalidateVisual(), DispatcherPriority.Render);
    }

    private void HandlePointerMoved(PointerEventArgs e)
    {
        if (DataContext is not PaintCanvasViewModel viewModel)
        {
            return;
        }

        // Use intermediate points to include past events we missed
        var points = e.GetIntermediatePoints(MainCanvas);

        if (points.Count == 0)
        {
            return;
        }

        viewModel.CurrentPenPressure = points.FirstOrDefault().Properties.Pressure;

        // Get or create a temp path
        if (!TemporaryPaths.TryGetValue(e.Pointer.Id, out var penPath))
        {
            penPath = new PenPath
            {
                FillColor = viewModel.PaintBrushSKColor.WithAlpha((byte)(viewModel.PaintBrushAlpha * 255)),
                IsErase = viewModel.SelectedTool == PaintCanvasTool.Eraser,
                Radius = (float)viewModel.PaintBrushSize,
            };
            TemporaryPaths[e.Pointer.Id] = penPath;
        }

        // Add line for path
        // var cursorPosition = e.GetPosition(MainCanvas);
        // penPath.Path.LineTo(cursorPosition.ToSKPoint());

        // Get bounds for discarding invalid points
        var canvasBounds = new Rect(0, 0, MainCanvas?.Bounds.Width ?? 0, MainCanvas?.Bounds.Height ?? 0);

        // Add points
        foreach (var point in points)
        {
            // Discard invalid points
            if (!canvasBounds.Contains(point.Position) || point.Position.X < 0 || point.Position.Y < 0)
            {
                continue;
            }

            var penPoint = new PenPoint(point.Position.X, point.Position.Y)
            {
                Pressure = point.Pointer.Type == PointerType.Mouse ? null : point.Properties.Pressure,
                Radius = viewModel.PaintBrushSize,
                IsPen = point.Pointer.Type == PointerType.Pen,
            };

            penPath.Points.Add(penPoint);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        HandlePointerEvent(e);
        base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        HandlePointerEvent(e);
        base.OnPointerReleased(e);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        HandlePointerEvent(e);
        base.OnPointerMoved(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            return;
        }

        // Keyboard shortcuts for paint canvas
        if (ViewModel is not { } vm)
            return;

        // Check for modifier keys
        var isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // Ctrl+Z: Undo
        if (isCtrl && e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (vm.UndoCommand.CanExecute(null))
            {
                vm.UndoCommand.Execute(null);
                RefreshCanvas();
            }
            e.Handled = true;
            return;
        }

        // Ctrl+Y or Ctrl+Shift+Z: Redo
        if (
            (isCtrl && e.Key == Key.Y)
            || (isCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Z)
        )
        {
            if (vm.RedoCommand.CanExecute(null))
            {
                vm.RedoCommand.Execute(null);
                RefreshCanvas();
            }
            e.Handled = true;
            return;
        }

        // Skip tool shortcuts if modifiers are held (to not interfere with other shortcuts)
        if (e.KeyModifiers != KeyModifiers.None)
            return;

        switch (e.Key)
        {
            case Key.B:
                vm.SelectBrushToolCommand.Execute(null);
                break;
            case Key.E:
                vm.SelectEraserToolCommand.Execute(null);
                break;
            case Key.R:
                vm.SelectRectangleToolCommand.Execute(null);
                break;
            case Key.O:
                vm.SelectEllipseToolCommand.Execute(null);
                break;
            case Key.OemOpenBrackets:
                vm.DecreaseBrushSizeCommand.Execute(null);
                break;
            case Key.OemCloseBrackets:
                vm.IncreaseBrushSizeCommand.Execute(null);
                break;
            case Key.G:
                vm.SelectPaintBucketToolCommand.Execute(null);
                break;
            default:
                return;
        }
        UpdateCanvasCursor();
        e.Handled = true;
    }

    /// <summary>
    /// Update the bounds of the main canvas to match the background image
    /// </summary>
    private void UpdateMainCanvasBounds()
    {
        if (MainCanvas is null || DataContext is not PaintCanvasViewModel vm)
        {
            return;
        }

        var canvasSize = vm.CanvasSize;

        // Set size if mismatch
        if (
            ((int)Math.Round(MainCanvas.Width) != canvasSize.Width)
            || ((int)Math.Round(MainCanvas.Height) != canvasSize.Height)
        )
        {
            MainCanvas.Width = vm.CanvasSize.Width;
            MainCanvas.Height = vm.CanvasSize.Height;
            MainCanvas.InvalidateVisual();
        }
    }

    private int lastCanvasCursorRadius;
    private Cursor? lastCanvasCursor;
    private PaintCanvasTool? lastCanvasCursorTool;

    private void UpdateCanvasCursor()
    {
        if (MainCanvas is not { } canvas)
        {
            return;
        }

        var selectedTool = ViewModel?.SelectedTool ?? PaintCanvasTool.PaintBrush;

        // Use crosshair for shape tools and paint bucket
        if (
            selectedTool
            is PaintCanvasTool.Rectangle
                or PaintCanvasTool.Ellipse
                or PaintCanvasTool.PaintBucket
        )
        {
            if (lastCanvasCursorTool != selectedTool)
            {
                lastCanvasCursor?.Dispose();
                lastCanvasCursor = new Cursor(StandardCursorType.Cross);
                lastCanvasCursorTool = selectedTool;
            }
            canvas.Cursor = lastCanvasCursor;
            return;
        }

        var currentZoom = ViewModel?.CurrentZoom ?? 1;

        // Get brush size
        var currentBrushSize = Math.Max((ViewModel?.PaintBrushSize ?? 1) - 2, 1);
        var brushRadius = (int)Math.Ceiling(currentBrushSize * 2 * currentZoom);

        // Only update cursor if brush size has changed
        if (brushRadius == lastCanvasCursorRadius && lastCanvasCursorTool == selectedTool)
        {
            canvas.Cursor = lastCanvasCursor;
            return;
        }

        lastCanvasCursorRadius = brushRadius;
        lastCanvasCursorTool = selectedTool;

        var brushDiameter = brushRadius * 2;

        const int padding = 4;

        var canvasCenter = brushRadius + padding;
        var canvasSize = brushDiameter + padding * 2;

        using var cursorBitmap = new SKBitmap(canvasSize, canvasSize);

        using var cursorCanvas = new SKCanvas(cursorBitmap);
        cursorCanvas.Clear(SKColors.Transparent);
        cursorCanvas.DrawCircle(
            brushRadius + padding,
            brushRadius + padding,
            brushRadius,
            new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsDither = true,
                IsAntialias = true,
            }
        );
        cursorCanvas.Flush();

        using var data = cursorBitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();

        var bitmap = WriteableBitmap.Decode(stream);

        canvas.Cursor = new Cursor(bitmap, new PixelPoint(canvasCenter, canvasCenter));

        lastCanvasCursor?.Dispose();
        lastCanvasCursor = canvas.Cursor;
    }

    private void MainCanvas_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        UpdateCanvasCursor();
    }

    private void MainCanvas_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is SkiaCustomCanvas canvas)
        {
            canvas.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }

    private void OnRenderSkia(SKSurface surface)
    {
        ViewModel?.RenderToSurface(surface, renderBackgroundFill: true, renderBackgroundImage: true);
    }
}
