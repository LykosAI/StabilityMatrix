using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.ViewModels.Controls;

namespace StabilityMatrix.Avalonia.Controls;

public class PaintCanvas : TemplatedControl
{
    private readonly ConcurrentDictionary<long, PenPath> temporaryPaths = new();
    private ImmutableList<PenPath> paths = [];

    private IDisposable? viewModelSubscription;

    private bool isPenDown;

    private PaintCanvasViewModel? ViewModel { get; set; }

    public SKBitmap? BackgroundBitmap { get; set; }

    private SkiaCustomCanvas? MainCanvas { get; set; }

    static PaintCanvas()
    {
        AffectsRender<PaintCanvas>(BoundsProperty);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        MainCanvas = e.NameScope.Find<SkiaCustomCanvas>("PART_MainCanvas");

        Debug.Assert(MainCanvas != null);

        if (MainCanvas is not null)
        {
            // If we already have a BackgroundBitmap, scale MainCanvas to match
            if (BackgroundBitmap is not null)
            {
                MainCanvas.Width = BackgroundBitmap.Width;
                MainCanvas.Height = BackgroundBitmap.Height;
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
        }

        OnDataContextChanged(EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is PaintCanvasViewModel viewModel)
        {
            // Set the save action
            viewModel.SaveCanvasAsImage = SaveCanvasToBitmap;

            viewModelSubscription?.Dispose();
            viewModelSubscription = viewModel
                .WhenPropertyChanged(vm => vm.BackgroundImage)
                .Subscribe(change =>
                {
                    if (MainCanvas is not null && change.Value is not null)
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
    }

    private void HandlePointerEvent(PointerEventArgs e)
    {
        if (e.RoutedEvent == PointerReleasedEvent && e.Pointer.Type == PointerType.Touch)
        {
            temporaryPaths.TryRemove(e.Pointer.Id, out _);
            return;
        }

        // if (e.Pointer.Type != PointerType.Pen || lastPointer.Properties.Pressure > 0)

        e.Handled = true;

        // Must have this or stylus inputs lost after a while
        // https://github.com/AvaloniaUI/Avalonia/issues/12289#issuecomment-1695620412

        e.PreventGestureRecognition();

        if (DataContext is not PaintCanvasViewModel viewModel)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(this);

        if (e.RoutedEvent == PointerPressedEvent)
        {
            // Ignore if mouse and not left button
            if (e.Pointer.Type == PointerType.Mouse && !currentPoint.Properties.IsLeftButtonPressed)
            {
                return;
            }

            isPenDown = true;

            var cursorPosition = e.GetPosition(MainCanvas);

            // Start a new path
            var path = new SKPath();
            path.MoveTo(cursorPosition.ToSKPoint());

            temporaryPaths[e.Pointer.Id] = new PenPath(path)
            {
                FillColor = viewModel.PaintBrushSKColor.WithAlpha((byte)(viewModel.PaintBrushAlpha * 255))
            };
        }
        else if (e.RoutedEvent == PointerReleasedEvent)
        {
            if (isPenDown)
            {
                isPenDown = false;
            }

            if (temporaryPaths.TryGetValue(e.Pointer.Id, out var path))
            {
                paths = paths.Add(path);
            }

            temporaryPaths.TryRemove(e.Pointer.Id, out _);
        }
        else
        {
            // Moved event
            if (!isPenDown || currentPoint.Properties.Pressure == 0)
            {
                return;
            }

            // Use intermediate points to include past events we missed
            var points = e.GetIntermediatePoints(MainCanvas);

            viewModel.CurrentPenPressure = points.FirstOrDefault().Properties.Pressure;

            // Get existing temp path
            if (temporaryPaths.TryGetValue(e.Pointer.Id, out var penPath))
            {
                var cursorPosition = e.GetPosition(MainCanvas);

                // Add line for path
                penPath.Path.LineTo(cursorPosition.ToSKPoint());

                // Add points
                foreach (var point in points)
                {
                    var skCanvasPoint = point.Position.ToSKPoint();

                    // penPath.Path.LineTo(skCanvasPoint);

                    var penPoint = new PenPoint(skCanvasPoint)
                    {
                        Pressure = point.Pointer.Type != PointerType.Mouse ? 1 : point.Properties.Pressure,
                        Radius = viewModel.PaintBrushSize,
                        IsPen = point.Pointer.Type == PointerType.Pen
                    };

                    penPath.Points.Add(penPoint);
                }
            }
        }

        Dispatcher.UIThread.Post(() => MainCanvas?.InvalidateVisual(), DispatcherPriority.Render);
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
        }
    }

    private int lastCanvasCursorRadius;
    private Cursor? lastCanvasCursor;

    private void UpdateCanvasCursor()
    {
        if (MainCanvas is not { } canvas)
        {
            return;
        }

        var currentZoom = ViewModel?.CurrentZoom ?? 1;

        // Get brush size
        var currentBrushSize = Math.Max(ViewModel?.PaintBrushSize ?? 1, 1);
        var brushPixels = (int)Math.Ceiling(currentBrushSize * 2 * currentZoom);
        var brushCanvasPixels = brushPixels * 2;

        // Only update cursor if brush size has changed
        if (brushCanvasPixels == lastCanvasCursorRadius)
        {
            canvas.Cursor = lastCanvasCursor;
            return;
        }

        lastCanvasCursorRadius = brushCanvasPixels;

        using var cursorBitmap = new SKBitmap(brushCanvasPixels, brushCanvasPixels);
        using var cursorCanvas = new SKCanvas(cursorBitmap);
        cursorCanvas.Clear(SKColors.Transparent);
        cursorCanvas.DrawCircle(
            brushPixels,
            brushPixels,
            brushPixels,
            new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsDither = true,
                IsAntialias = true
            }
        );
        cursorCanvas.Flush();

        using var data = cursorBitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();

        var bitmap = WriteableBitmap.Decode(stream);

        canvas.Cursor = new Cursor(bitmap, new PixelPoint(brushCanvasPixels / 2, brushCanvasPixels / 2));

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

    private Point GetRelativePosition(Point pt, Visual? relativeTo)
    {
        if (VisualRoot is not Visual visualRoot)
            return default;
        if (relativeTo == null)
            return pt;

        return pt * visualRoot.TransformToVisual(relativeTo) ?? default;
    }

    public void SaveCanvasToBitmap(Stream stream)
    {
        using var surface = SKSurface.Create(new SKImageInfo((int)Bounds.Width, (int)Bounds.Height));
        using var canvas = surface.Canvas;

        OnRenderSkia(canvas);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
    }

    public AsyncRelayCommand ClearCanvasCommand => new(ClearCanvasAsync);

    public async Task ClearCanvasAsync()
    {
        paths = ImmutableList<PenPath>.Empty;
        temporaryPaths.Clear();

        await Dispatcher.UIThread.InvokeAsync(() => MainCanvas?.InvalidateVisual());
    }

    private static void RenderPenPath(SKCanvas canvas, PenPath penPath, SKPaint paint)
    {
        // Apply Color
        paint.Color = penPath.FillColor;
        // Defaults
        paint.IsDither = true;
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;

        // Can't use foreach since this list may be modified during iteration
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < penPath.Points.Count; i++)
        {
            var penPoint = penPath.Points[i];

            var radius = penPoint.Radius;
            var pressure = penPoint.Pressure ?? 1;
            var thickness = pressure * radius * 2;
            // var radius = pressure * penPoint.Radius * 1.5;

            // Draw path
            if (i < penPath.Points.Count - 1)
            {
                paint.StrokeWidth = (float)thickness;
                canvas.DrawLine(penPoint.Point, penPath.Points[i + 1].Point, paint);
            }

            // Only draw circle if pressure is high enough
            if (penPoint.Pressure > 0.1)
            {
                paint.StrokeWidth = (float)(thickness * 0.5);
                canvas.DrawCircle(penPoint.Point, (float)radius, paint);
            }
        }
    }

    private void OnRenderSkia(SKCanvas canvas)
    {
        // Draw background color
        canvas.Clear(SKColors.Transparent);

        // Draw background image if set
        if (ViewModel?.BackgroundImage is { } backgroundImage)
        {
            canvas.DrawBitmap(backgroundImage, new SKPoint(0, 0));
        }

        using var paint = new SKPaint();

        // Draw the paths
        foreach (var penPath in temporaryPaths.Values)
        {
            RenderPenPath(canvas, penPath, paint);
        }

        foreach (var penPath in paths)
        {
            RenderPenPath(canvas, penPath, paint);
        }

        canvas.Flush();
    }
}
