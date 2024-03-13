using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;

namespace StabilityMatrix.Avalonia.Controls;

public class PaintCanvas : TemplatedControl
{
    private readonly ConcurrentDictionary<long, PenPath> temporaryPaths = new();
    private ImmutableList<PenPath> paths = [];

    private bool isPenDown;
    private SKColor currentBrushColor = Colors.White.ToSKColor();

    private SkiaCustomCanvas? MainCanvas { get; set; }

    public static readonly StyledProperty<Color?> PaintBrushColorProperty = AvaloniaProperty.Register<
        PaintCanvas,
        Color?
    >(nameof(PaintBrushColor), Colors.White);

    public Color? PaintBrushColor
    {
        get => GetValue(PaintBrushColorProperty);
        set => SetValue(PaintBrushColorProperty, value);
    }

    public static readonly StyledProperty<float> CurrentPenPressureProperty = AvaloniaProperty.Register<
        PaintCanvas,
        float
    >("CurrentPenPressure");

    public float CurrentPenPressure
    {
        get => GetValue(CurrentPenPressureProperty);
        set => SetValue(CurrentPenPressureProperty, value);
    }

    static PaintCanvas()
    {
        AffectsRender<ImageMaskEditor>(BoundsProperty);
    }

    public PaintCanvas()
    {
        // AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PaintBrushColorProperty)
        {
            currentBrushColor = (PaintBrushColor ?? Colors.Transparent).ToSKColor();
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        MainCanvas = e.NameScope.Find<SkiaCustomCanvas>("PART_MainCanvas");

        Debug.Assert(MainCanvas != null);

        if (MainCanvas is not null)
        {
            MainCanvas.RenderSkia += OnRenderSkia;
        }
    }

    private void HandlePointerEvent(PointerEventArgs e)
    {
        var lastPointer = e.GetCurrentPoint(this);

        if (e.RoutedEvent == PointerReleasedEvent && e.Pointer.Type == PointerType.Touch)
        {
            temporaryPaths.TryRemove(e.Pointer.Id, out _);
            return;
        }

        // if (e.Pointer.Type != PointerType.Pen || lastPointer.Properties.Pressure > 0)
        if (true)
        {
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

                isPenDown = true;

                var cursorPosition = e.GetPosition(MainCanvas);

                // Start a new path
                var path = new SKPath();
                path.MoveTo(cursorPosition.ToSKPoint());

                temporaryPaths[e.Pointer.Id] = new PenPath(path) { FillColor = currentBrushColor };
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

                CurrentPenPressure = points.FirstOrDefault().Properties.Pressure;

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

                        var penPoint = new PenPoint(skCanvasPoint) { Pressure = point.Properties.Pressure };

                        penPath.Points.Add(penPoint);
                    }
                }
            }

            Dispatcher.UIThread.Post(() => MainCanvas!.InvalidateVisual(), DispatcherPriority.Render);
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

    private static void RenderPenPath(SKCanvas canvas, PenPath penPath)
    {
        using var paint = new SKPaint();
        paint.Color = penPath.FillColor;
        paint.IsDither = true;
        paint.IsAntialias = true;
        paint.StrokeWidth = 3;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;

        // Can't use foreach since this list may be modified during iteration
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < penPath.Points.Count; i++)
        {
            var penPoint = penPath.Points[i];

            var pressure = penPoint.Pressure ?? 0.5;
            var thickness = pressure * 10;
            var radius = pressure * penPoint.Radius * 1.5;

            // Draw path
            if (i < penPath.Points.Count - 1)
            {
                paint.StrokeWidth = (float)thickness;
                canvas.DrawLine(penPoint.Point, penPath.Points[i + 1].Point, paint);
            }

            // Only draw circle if pressure is high enough
            if (penPoint.Pressure > 0.1)
            {
                canvas.DrawCircle(penPoint.Point, (float)radius, paint);
            }
        }
    }

    public void OnRenderSkia(SKCanvas canvas)
    {
        // canvas.Clear();

        SKPaint? paint = null;

        try
        {
            // Draw the paths
            foreach (var penPath in temporaryPaths.Values)
            {
                RenderPenPath(canvas, penPath);
            }

            foreach (var penPath in paths)
            {
                RenderPenPath(canvas, penPath);
            }

            /*foreach (var penPath in temporaryPaths.Values)
            {
                if (paint?.Color != penPath.FillColor)
                {
                    paint?.Dispose();
            
                    paint = new SKPaint();
                    paint.Color = penPath.FillColor;
                    paint.IsDither = true;
                    paint.IsAntialias = true;
                    paint.StrokeWidth = 3;
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeCap = SKStrokeCap.Round;
                }
            
                canvas.DrawPath(penPath.Path, paint);
            }
            
            foreach (var penPath in paths)
            {
                if (paint?.Color != penPath.FillColor)
                {
                    paint?.Dispose();
            
                    paint = new SKPaint();
                    paint.Color = penPath.FillColor;
                    paint.IsDither = true;
                    paint.IsAntialias = true;
                    paint.StrokeWidth = 3;
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeCap = SKStrokeCap.Round;
                }
            
                canvas.DrawPath(penPath.Path, paint);
            }*/

            canvas.Flush();
        }
        finally
        {
            paint?.Dispose();
        }
    }
}
