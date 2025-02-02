using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using Color = Avalonia.Media.Color;
using Size = System.Drawing.Size;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

[RegisterTransient<PaintCanvasViewModel>]
[ManagedService]
public partial class PaintCanvasViewModel(ILogger<PaintCanvasViewModel> logger) : LoadableViewModelBase
{
    public ConcurrentDictionary<long, PenPath> TemporaryPaths { get; set; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private ImmutableList<PenPath> paths = [];

    [ObservableProperty]
    private Color? paintBrushColor = Colors.White;

    public SKColor PaintBrushSKColor => (PaintBrushColor ?? Colors.Transparent).ToSKColor();

    [ObservableProperty]
    private double paintBrushSize = 12;

    [ObservableProperty]
    private double paintBrushAlpha = 1;

    [ObservableProperty]
    private double currentPenPressure;

    [ObservableProperty]
    private double currentZoom;

    [ObservableProperty]
    private bool isPenDown;

    [ObservableProperty]
    private PaintCanvasTool selectedTool = PaintCanvasTool.PaintBrush;

    [ObservableProperty]
    private Size canvasSize = Size.Empty;

    [JsonIgnore]
    private SKCanvas? SourceCanvas { set; get; }

    [Localizable(false)]
    [JsonIgnore]
    private OrderedDictionary<string, SKLayer> Layers { get; } =
        new()
        {
            ["Background"] = new SKLayer(),
            ["Images"] = new SKLayer(),
            ["Brush"] = new SKLayer(),
        };

    [JsonIgnore]
    private SKLayer BrushLayer => Layers["Brush"];

    [JsonIgnore]
    private SKLayer ImagesLayer => Layers["Images"];

    [JsonIgnore]
    private SKLayer BackgroundLayer => Layers["Background"];

    [JsonIgnore]
    public SKBitmap? BackgroundImage
    {
        get => BackgroundLayer.Bitmaps.FirstOrDefault();
        set
        {
            if (value is not null)
            {
                CanvasSize = new Size(value.Width, value.Height);
                BackgroundLayer.Bitmaps = [value];
            }
            else
            {
                CanvasSize = Size.Empty;
                BackgroundLayer.Bitmaps = [];
            }
        }
    }

    /// <summary>
    /// Set by <see cref="PaintCanvas"/> to allow the view model to
    /// refresh the canvas view after updating points or bitmap layers.
    /// </summary>
    [JsonIgnore]
    public Action? RefreshCanvas { get; set; }

    public void SetSourceCanvas(SKCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas, nameof(canvas));
        SourceCanvas = canvas;
    }

    public void LoadCanvasFromBitmap(SKBitmap bitmap)
    {
        ImagesLayer.Bitmaps = [bitmap];

        RefreshCanvas?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
    public void Undo()
    {
        // Remove last path
        var currentPaths = Paths;

        if (currentPaths.IsEmpty)
        {
            return;
        }

        Paths = currentPaths.RemoveAt(currentPaths.Count - 1);

        RefreshCanvas?.Invoke();
    }

    private bool CanExecuteUndo()
    {
        return Paths.Count > 0;
    }

    public SKImage? RenderToWhiteChannelImage()
    {
        using var _ = CodeTimer.StartDebug();

        if (CanvasSize == Size.Empty)
        {
            logger.LogWarning($"RenderToImage: {nameof(CanvasSize)} is not set, returning null.");
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(CanvasSize.Width, CanvasSize.Height));

        RenderToSurface(surface);

        using var originalImage = surface.Snapshot();
        // Replace all colors to white (255, 255, 255), keep original alpha
        // csharpier-ignore
        using var colorFilter = SKColorFilter.CreateColorMatrix(
            [
                // R, G, B, A, Bias
                -1, 0, 0, 0, 255,
                0, -1, 0, 0, 255,
                0, 0, -1, 0, 255,
                0, 0, 0, 1, 0
            ]
        );

        using var paint = new SKPaint();
        paint.ColorFilter = colorFilter;

        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(originalImage, originalImage.Info.Rect, paint);

        return surface.Snapshot();
    }

    public SKImage? RenderToImage()
    {
        using var _ = CodeTimer.StartDebug();

        if (CanvasSize == Size.Empty)
        {
            logger.LogWarning($"RenderToImage: {nameof(CanvasSize)} is not set, returning null.");
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(CanvasSize.Width, CanvasSize.Height));

        RenderToSurface(surface);

        return surface.Snapshot();
    }

    public void RenderToSurface(
        SKSurface surface,
        bool renderBackgroundFill = false,
        bool renderBackgroundImage = false
    )
    {
        // Initialize canvas layers
        foreach (var layer in Layers.Values)
        {
            lock (layer)
            {
                if (layer.Surface is null)
                {
                    layer.Surface = SKSurface.Create(new SKImageInfo(CanvasSize.Width, CanvasSize.Height));
                    /*layer.Surface = SKSurface.Create(
                        surface.Context,
                        true,
                        new SKImageInfo(CanvasSize.Width, CanvasSize.Height)
                    );*/
                }
                else
                {
                    // If we need to resize:
                    var currentInfo = layer.Surface.Canvas.DeviceClipBounds;
                    if (currentInfo.Width != CanvasSize.Width || currentInfo.Height != CanvasSize.Height)
                    {
                        // Dispose the old surface
                        layer.Surface.Dispose();

                        // Create a brand-new SKSurface with the new size
                        layer.Surface = SKSurface.Create(
                            new SKImageInfo(CanvasSize.Width, CanvasSize.Height)
                        );
                    }
                    else
                    {
                        // No resize needed, just clear
                        layer.Surface.Canvas.Clear(SKColors.Transparent);
                    }
                }
            }
        }

        // Render all layer images in order
        foreach (var (layerName, layer) in Layers)
        {
            // Skip background image if not requested
            if (!renderBackgroundImage && layerName == "Background")
            {
                continue;
            }

            lock (layer)
            {
                var layerCanvas = layer.Surface!.Canvas;
                foreach (var bitmap in layer.Bitmaps)
                {
                    layerCanvas.DrawBitmap(bitmap, new SKPoint(0, 0));
                }
            }
        }

        // Render paint layer
        var paintLayerCanvas = BrushLayer.Surface!.Canvas;

        using var paint = new SKPaint();

        // Draw the paths
        foreach (var penPath in Paths)
        {
            RenderPenPath(paintLayerCanvas, penPath, paint);
        }

        foreach (var penPath in TemporaryPaths.Values)
        {
            RenderPenPath(paintLayerCanvas, penPath, paint);
        }

        // Draw background color
        surface.Canvas.Clear(SKColors.Transparent);

        // Draw the layers to the main surface
        foreach (var layer in Layers.Values)
        {
            lock (layer)
            {
                layer.Surface!.Canvas.Flush();

                surface.Canvas.DrawSurface(layer.Surface!, new SKPoint(0, 0));
            }
        }

        surface.Canvas!.Flush();
    }

    private static void RenderPenPath(SKCanvas canvas, PenPath penPath, SKPaint paint)
    {
        if (penPath.Points.Count == 0)
        {
            return;
        }

        // Apply Color
        if (penPath.IsErase)
        {
            // paint.BlendMode = SKBlendMode.SrcIn;
            paint.BlendMode = SKBlendMode.Clear;
            paint.Color = SKColors.Transparent;
        }
        else
        {
            paint.BlendMode = SKBlendMode.SrcOver;
            paint.Color = penPath.FillColor;
        }

        // Defaults
        paint.IsDither = true;
        paint.IsAntialias = true;

        // Track if we have any pen points
        var hasPenPoints = false;

        // Can't use foreach since this list may be modified during iteration
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < penPath.Points.Count; i++)
        {
            var penPoint = penPath.Points[i];

            // Skip non-pen points
            if (!penPoint.IsPen)
            {
                continue;
            }

            hasPenPoints = true;

            var radius = penPoint.Radius;
            var pressure = penPoint.Pressure ?? 1;
            var thickness = pressure * radius * 2.5;

            // Draw path
            if (i < penPath.Points.Count - 1)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = (float)thickness;
                paint.StrokeCap = SKStrokeCap.Round;
                paint.StrokeJoin = SKStrokeJoin.Round;

                var nextPoint = penPath.Points[i + 1];
                canvas.DrawLine(penPoint.X, penPoint.Y, nextPoint.X, nextPoint.Y, paint);
            }

            // Draw circles for pens
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(penPoint.X, penPoint.Y, (float)thickness / 2, paint);
        }

        // Draw paths directly if we didn't have any pen points
        if (!hasPenPoints)
        {
            var point = penPath.Points[0];
            var thickness = point.Radius * 2;

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = (float)thickness;
            paint.StrokeCap = SKStrokeCap.Round;
            paint.StrokeJoin = SKStrokeJoin.Round;

            var skPath = penPath.ToSKPath();
            canvas.DrawPath(skPath, paint);
        }
    }
}
