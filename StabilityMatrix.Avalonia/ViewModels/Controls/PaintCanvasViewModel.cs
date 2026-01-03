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

    /// <summary>
    /// Sets or clears a bitmap for the Images layer.
    /// Used for displaying other layers as a background when compositing.
    /// </summary>
    /// <param name="name">Identifier for the bitmap (currently ignored, single bitmap only)</param>
    /// <param name="bitmap">The bitmap to set, or null to clear</param>
    public void SetLayerBitmap(string name, SKBitmap? bitmap)
    {
        if (bitmap is not null)
        {
            Layers["Images"].Bitmaps = [bitmap];
        }
        else
        {
            Layers["Images"].Bitmaps = [];
        }
    }

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

    /// <summary>
    /// Extracts masks for multiple colors in a single render pass.
    /// More efficient than calling ExtractMaskByColor multiple times.
    /// </summary>
    /// <param name="targetColors">The colors to extract masks for.</param>
    /// <param name="tolerance">RGB tolerance for color matching (0-255). Default 10.</param>
    /// <returns>A dictionary mapping each color to its mask image.</returns>
    public Dictionary<SKColor, SKImage> ExtractMasksByColors(
        IReadOnlyList<SKColor> targetColors,
        int tolerance = 10
    )
    {
        using var _ = CodeTimer.StartDebug();

        var results = new Dictionary<SKColor, SKImage>();

        if (CanvasSize == Size.Empty || targetColors.Count == 0)
            return results;

        // Render canvas once
        using var renderedImage = RenderToImage();
        if (renderedImage is null)
            return results;

        using var sourceBitmap = SKBitmap.FromImage(renderedImage);
        var srcPixels = sourceBitmap.Pixels; // SKColor[] array - fast direct access
        var pixelCount = srcPixels.Length;

        // Create result bitmaps for each color
        var resultBitmaps = new Dictionary<SKColor, SKBitmap>();
        var resultPixels = new Dictionary<SKColor, SKColor[]>();
        foreach (var color in targetColors)
        {
            var bitmap = new SKBitmap(
                sourceBitmap.Width,
                sourceBitmap.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul
            );
            resultBitmaps[color] = bitmap;
            resultPixels[color] = new SKColor[pixelCount];
        }

        // Single pass through pixels, check all colors
        for (var i = 0; i < pixelCount; i++)
        {
            var pixel = srcPixels[i];

            foreach (var targetColor in targetColors)
            {
                var matches =
                    Math.Abs(pixel.Red - targetColor.Red) <= tolerance
                    && Math.Abs(pixel.Green - targetColor.Green) <= tolerance
                    && Math.Abs(pixel.Blue - targetColor.Blue) <= tolerance
                    && pixel.Alpha > 0;

                resultPixels[targetColor][i] = matches ? SKColors.White : SKColors.Transparent;
            }
        }

        // Set pixels and convert bitmaps to images
        foreach (var (color, bitmap) in resultBitmaps)
        {
            bitmap.Pixels = resultPixels[color];
            results[color] = SKImage.FromBitmap(bitmap);
            bitmap.Dispose();
        }

        return results;
    }

    /// <summary>
    /// Extracts a mask from the canvas where pixels match the target color.
    /// Returns a grayscale mask where white = match, transparent = no match.
    /// Used for regional prompting to separate painted regions by color.
    /// </summary>
    /// <param name="targetColor">The color to extract.</param>
    /// <param name="tolerance">RGB tolerance for color matching (0-255). Default 10.</param>
    /// <returns>A mask image, or null if canvas is empty.</returns>
    public SKImage? ExtractMaskByColor(SKColor targetColor, int tolerance = 10)
    {
        using var _ = CodeTimer.StartDebug();

        if (CanvasSize == Size.Empty)
        {
            logger.LogWarning($"ExtractMaskByColor: {nameof(CanvasSize)} is not set, returning null.");
            return null;
        }

        // First render the canvas to get the painted image
        using var renderedImage = RenderToImage();
        if (renderedImage is null)
            return null;

        using var bitmap = SKBitmap.FromImage(renderedImage);
        var resultBitmap = new SKBitmap(
            bitmap.Width,
            bitmap.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );

        // Use Pixels array for fast direct access
        var srcPixels = bitmap.Pixels;
        var dstPixels = new SKColor[srcPixels.Length];

        for (var i = 0; i < srcPixels.Length; i++)
        {
            var pixel = srcPixels[i];

            // Check if pixel matches target color within tolerance
            var matches =
                Math.Abs(pixel.Red - targetColor.Red) <= tolerance
                && Math.Abs(pixel.Green - targetColor.Green) <= tolerance
                && Math.Abs(pixel.Blue - targetColor.Blue) <= tolerance
                && pixel.Alpha > 0;

            dstPixels[i] = matches ? SKColors.White : SKColors.Transparent;
        }

        resultBitmap.Pixels = dstPixels;
        return SKImage.FromBitmap(resultBitmap);
    }

    /// <summary>
    /// Gets all unique colors present in the painted canvas (excluding transparent).
    /// Used for regional prompting to detect which colors the user has painted.
    /// </summary>
    /// <returns>A list of unique colors found in the canvas.</returns>
    public IReadOnlyList<SKColor> GetPaintedColors()
    {
        // Default palette colors to match against
        return GetPaintedColors(
            [
                new SKColor(255, 0, 0), // Red
                new SKColor(255, 128, 0), // Orange
                new SKColor(255, 255, 0), // Yellow
                new SKColor(0, 255, 0), // Green
                new SKColor(0, 128, 255), // Blue
                new SKColor(128, 0, 255), // Purple
            ]
        );
    }

    /// <summary>
    /// Gets a list of palette colors that have been painted on the canvas.
    /// Uses tolerance matching to handle anti-aliased edges.
    /// </summary>
    /// <param name="paletteColors">The palette colors to match against.</param>
    /// <param name="tolerance">RGB tolerance for color matching (default 40 to handle anti-aliasing).</param>
    /// <returns>A list of palette colors that were found in the canvas.</returns>
    public IReadOnlyList<SKColor> GetPaintedColors(IReadOnlyList<SKColor> paletteColors, int tolerance = 40)
    {
        if (CanvasSize == Size.Empty)
            return [];

        using var renderedImage = RenderToImage();
        if (renderedImage is null)
            return [];

        using var bitmap = SKBitmap.FromImage(renderedImage);
        var foundPaletteColors = new HashSet<SKColor>();

        // Use Pixels array for fast direct access
        var pixels = bitmap.Pixels;
        var paletteCount = paletteColors.Count;

        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (pixel.Alpha < 128) // Skip mostly transparent pixels
                continue;

            // Find the closest palette color
            for (var p = 0; p < paletteCount; p++)
            {
                var paletteColor = paletteColors[p];
                if (ColorMatchesWithTolerance(pixel, paletteColor, tolerance))
                {
                    foundPaletteColors.Add(paletteColor);

                    // Early exit if we've found all palette colors
                    if (foundPaletteColors.Count == paletteCount)
                        return foundPaletteColors.ToList();

                    break;
                }
            }
        }

        return foundPaletteColors.ToList();
    }

    /// <summary>
    /// Checks if two colors match within the specified RGB tolerance.
    /// </summary>
    private static bool ColorMatchesWithTolerance(SKColor a, SKColor b, int tolerance)
    {
        return Math.Abs(a.Red - b.Red) <= tolerance
            && Math.Abs(a.Green - b.Green) <= tolerance
            && Math.Abs(a.Blue - b.Blue) <= tolerance;
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
