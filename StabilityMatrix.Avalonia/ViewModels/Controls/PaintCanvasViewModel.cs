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

    /// <summary>
    /// Stack of undone paths for redo functionality.
    /// </summary>
    [JsonIgnore]
    private readonly Stack<PenPath> redoStack = new();

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

    /// <summary>
    /// Whether drawing is enabled. Set to false to disable brush strokes (e.g., for image reference layers).
    /// </summary>
    [ObservableProperty]
    private bool isDrawingEnabled = true;

    /// <summary>
    /// Whether to draw shapes (Rectangle/Ellipse) as strokes only instead of filled.
    /// </summary>
    [ObservableProperty]
    private bool isShapeStrokeOnly;

    [JsonIgnore]
    private SKCanvas? SourceCanvas { set; get; }

    [Localizable(false)]
    [JsonIgnore]
    private OrderedDictionary<string, SKLayer> Layers { get; } =
        new()
        {
            ["Background"] = new SKLayer(),
            ["Images"] = new SKLayer(), // Layers BELOW the selected layer
            ["Brush"] = new SKLayer(), // The currently selected/active layer
            ["Overlay"] = new SKLayer(), // Layers ABOVE the selected layer
        };

    [JsonIgnore]
    private SKLayer BrushLayer => Layers["Brush"];

    [JsonIgnore]
    private SKLayer ImagesLayer => Layers["Images"];

    [JsonIgnore]
    private SKLayer OverlayLayer => Layers["Overlay"];

    [JsonIgnore]
    private SKLayer BackgroundLayer => Layers["Background"];

    /// <summary>
    /// Cached bitmap of all finalized paths. Cleared when paths change.
    /// </summary>
    [JsonIgnore]
    private SKImage? cachedPathsImage;

    /// <summary>
    /// Number of paths that were rendered into the cached image.
    /// Used to determine if cache needs to be updated.
    /// </summary>
    [JsonIgnore]
    private int cachedPathsCount;

    /// <summary>
    /// Cached surface for temporary paths during active drawing.
    /// Allows incremental rendering of long strokes.
    /// </summary>
    [JsonIgnore]
    private SKSurface? tempPathSurface;

    /// <summary>
    /// Tracks how many points have been rendered to the temp path surface per pointer ID.
    /// </summary>
    [JsonIgnore]
    private readonly ConcurrentDictionary<long, int> tempPathRenderedPoints = new();

    /// <summary>
    /// Stored GPU context for creating GPU-backed surfaces.
    /// Updated each render frame from the main surface.
    /// </summary>
    [JsonIgnore]
    private GRRecordingContext? currentGrContext;

    /// <summary>
    /// Whether to use GPU-accelerated surfaces when available.
    /// </summary>
    [JsonIgnore]
    public bool UseGpuAcceleration { get; set; } = true;

    /// <summary>
    /// Indicates whether GPU acceleration is currently active.
    /// </summary>
    [JsonIgnore]
    public bool IsUsingGpu { get; private set; }

    /// <summary>
    /// Debug flag: Set to true to log GPU/CPU surface creation.
    /// </summary>
    [JsonIgnore]
    public static bool LogRenderingMode { get; set; }
#if DEBUG
        = true;
#endif

    /// <summary>
    /// Whether to show a checkerboard pattern for transparent areas.
    /// </summary>
    [JsonIgnore]
    public bool ShowCheckerboardBackground { get; set; } = true;

    /// <summary>
    /// Size of each checkerboard square in pixels.
    /// </summary>
    private const int CheckerboardSquareSize = 16;

    /// <summary>
    /// Light color for the checkerboard pattern.
    /// </summary>
    private static readonly SKColor CheckerboardLight = new(220, 220, 220);

    /// <summary>
    /// Dark color for the checkerboard pattern.
    /// </summary>
    private static readonly SKColor CheckerboardDark = new(180, 180, 180);

    /// <summary>
    /// Cached checkerboard shader for efficient rendering.
    /// </summary>
    [JsonIgnore]
    private SKShader? cachedCheckerboardShader;

    /// <summary>
    /// The canvas size that the cached checkerboard shader was created for.
    /// </summary>
    [JsonIgnore]
    private Size cachedCheckerboardSize;

    /// <summary>
    /// Whether to show a grid overlay for alignment assistance.
    /// </summary>
    [ObservableProperty]
    private bool showGridOverlay;

    /// <summary>
    /// Number of grid divisions (e.g., 3 for rule of thirds).
    /// </summary>
    [ObservableProperty]
    private int gridDivisions = 3;

    /// <summary>
    /// Color for the grid overlay lines.
    /// </summary>
    private static readonly SKColor GridLineColor = new(128, 128, 128, 180);

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
    /// Sets or clears a bitmap for a compositing layer.
    /// Used for displaying other layers when compositing in a layered editor.
    /// </summary>
    /// <param name="name">
    /// Layer name: "Images" for layers below the selected layer,
    /// "Overlay" for layers above the selected layer,
    /// or legacy "OtherLayers" which maps to "Images" for backwards compatibility.
    /// </param>
    /// <param name="bitmap">The bitmap to set, or null to clear</param>
    public void SetLayerBitmap(string name, SKBitmap? bitmap)
    {
        // Map legacy name to new name for backwards compatibility
        var layerName = name switch
        {
            "OtherLayers" => "Images", // Legacy: all other layers went to Images
            "LayersBelow" => "Images",
            "LayersAbove" => "Overlay",
            "CurrentImage" => "Brush", // Selected image layer bitmap goes to Brush layer
            _ => name,
        };

        if (!Layers.ContainsKey(layerName))
        {
            return;
        }

        if (bitmap is not null)
        {
            Layers[layerName].Bitmaps = [bitmap];
        }
        else
        {
            Layers[layerName].Bitmaps = [];
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

        // Push the removed path to redo stack
        var removedPath = currentPaths[^1];
        redoStack.Push(removedPath);
        RedoCommand.NotifyCanExecuteChanged();

        Paths = currentPaths.RemoveAt(currentPaths.Count - 1);

        // Invalidate cache since paths changed
        InvalidatePathCache();

        RefreshCanvas?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteRedo))]
    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        var pathToRestore = redoStack.Pop();
        Paths = Paths.Add(pathToRestore);
        RedoCommand.NotifyCanExecuteChanged();

        // Invalidate cache since paths changed
        InvalidatePathCache();

        RefreshCanvas?.Invoke();
    }

    /// <summary>
    /// Invalidates the cached paths image. Call when paths are modified externally.
    /// </summary>
    public void InvalidatePathCache()
    {
        cachedPathsImage?.Dispose();
        cachedPathsImage = null;
        cachedPathsCount = 0;
    }

    /// <summary>
    /// Called when the Paths property changes.
    /// Invalidates the cache since we have a completely new set of paths.
    /// </summary>
    partial void OnPathsChanged(ImmutableList<PenPath> value)
    {
        // When paths change (e.g., layer switch), invalidate the cache
        // since the cached image is from the old paths
        InvalidatePathCache();
    }

    private bool CanExecuteUndo()
    {
        return Paths.Count > 0;
    }

    private bool CanExecuteRedo()
    {
        return redoStack.Count > 0;
    }

    /// <summary>
    /// Clears the redo stack. Call when new paths are added (not via redo).
    /// </summary>
    public void ClearRedoStack()
    {
        if (redoStack.Count > 0)
        {
            redoStack.Clear();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }

    #region Shape Tool State

    /// <summary>
    /// Starting point for shape drawing (Rectangle/Ellipse tools).
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private SKPoint? shapeStartPoint;

    /// <summary>
    /// Pointer ID for the current shape drawing operation.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private long shapePointerId;

    /// <summary>
    /// Returns true if the currently selected tool is a shape tool.
    /// </summary>
    [JsonIgnore]
    public bool IsShapeTool => SelectedTool is PaintCanvasTool.Rectangle or PaintCanvasTool.Ellipse;

    #endregion

    #region Canvas Commands

    /// <summary>
    /// Clears all paths from the canvas.
    /// </summary>
    [RelayCommand]
    public void ClearCanvas()
    {
        Paths = ImmutableList<PenPath>.Empty;
        TemporaryPaths.Clear();
        redoStack.Clear();
        RedoCommand.NotifyCanExecuteChanged();
        InvalidatePathCache();
        RefreshCanvas?.Invoke();
    }

    #endregion

    #region Tool Selection Commands

    [RelayCommand]
    public void SelectBrushTool() => SelectedTool = PaintCanvasTool.PaintBrush;

    [RelayCommand]
    public void SelectEraserTool() => SelectedTool = PaintCanvasTool.Eraser;

    [RelayCommand]
    public void SelectRectangleTool() => SelectedTool = PaintCanvasTool.Rectangle;

    [RelayCommand]
    public void SelectEllipseTool() => SelectedTool = PaintCanvasTool.Ellipse;

    #endregion

    #region Brush Size Commands

    [RelayCommand]
    public void IncreaseBrushSize()
    {
        PaintBrushSize = Math.Min(100, PaintBrushSize + 5);
    }

    [RelayCommand]
    public void DecreaseBrushSize()
    {
        PaintBrushSize = Math.Max(1, PaintBrushSize - 5);
    }

    #endregion

    #region Shape Drawing Helpers

    /// <summary>
    /// Starts shape drawing at the given position.
    /// </summary>
    public void StartShapeDrawing(SKPoint position, long pointerId)
    {
        ShapeStartPoint = position;
        ShapePointerId = pointerId;
    }

    /// <summary>
    /// Updates the shape preview during drag.
    /// </summary>
    public void UpdateShapePreview(SKPoint currentPoint)
    {
        if (!ShapeStartPoint.HasValue)
            return;

        var bounds = CreateBoundsFromPoints(ShapeStartPoint.Value, currentPoint);
        var previewPath = new PenPath
        {
            FillColor = PaintBrushSKColor.WithAlpha((byte)(PaintBrushAlpha * 255)),
            PathType =
                SelectedTool == PaintCanvasTool.Rectangle ? PenPathType.Rectangle : PenPathType.Ellipse,
            Bounds = bounds,
            IsStrokeOnly = IsShapeStrokeOnly,
            StrokeWidth = (float)PaintBrushSize,
        };
        TemporaryPaths[ShapePointerId] = previewPath;
    }

    /// <summary>
    /// Finalizes the shape drawing and adds it to paths.
    /// </summary>
    /// <returns>The created shape path, or null if shape was too small.</returns>
    public PenPath? FinalizeShape(SKPoint endPoint)
    {
        if (!ShapeStartPoint.HasValue)
            return null;

        var bounds = CreateBoundsFromPoints(ShapeStartPoint.Value, endPoint);

        // Only create shape if it has meaningful size
        if (bounds.Width <= 2 || bounds.Height <= 2)
        {
            ShapeStartPoint = null;
            TemporaryPaths.TryRemove(ShapePointerId, out _);
            return null;
        }

        var shapePath = new PenPath
        {
            FillColor = PaintBrushSKColor.WithAlpha((byte)(PaintBrushAlpha * 255)),
            IsErase = SelectedTool == PaintCanvasTool.Eraser,
            PathType =
                SelectedTool == PaintCanvasTool.Rectangle ? PenPathType.Rectangle : PenPathType.Ellipse,
            Bounds = bounds,
            IsStrokeOnly = IsShapeStrokeOnly,
            StrokeWidth = (float)PaintBrushSize,
        };

        Paths = Paths.Add(shapePath);
        ClearRedoStack(); // New path added, clear redo history
        ShapeStartPoint = null;
        TemporaryPaths.TryRemove(ShapePointerId, out _);

        return shapePath;
    }

    /// <summary>
    /// Cancels the current shape drawing operation.
    /// </summary>
    public void CancelShapeDrawing()
    {
        ShapeStartPoint = null;
        TemporaryPaths.TryRemove(ShapePointerId, out _);
    }

    private static SKRect CreateBoundsFromPoints(SKPoint start, SKPoint end)
    {
        return new SKRect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Max(start.X, end.X),
            Math.Max(start.Y, end.Y)
        );
    }

    #endregion

    #region Paint Bucket / Flood Fill

    [RelayCommand]
    public void SelectPaintBucketTool() => SelectedTool = PaintCanvasTool.PaintBucket;

    /// <summary>
    /// Performs a flood fill at the specified point.
    /// Returns the created path, or null if fill wasn't possible.
    /// </summary>
    public PenPath? FloodFillAt(SKPoint clickPoint, SKColor fillColor)
    {
        if (CanvasSize == Size.Empty)
            return null;

        var x = (int)clickPoint.X;
        var y = (int)clickPoint.Y;

        // Bounds check
        if (x < 0 || x >= CanvasSize.Width || y < 0 || y >= CanvasSize.Height)
            return null;

        // Get the current state of the canvas on CPU to avoid GPU context threading issues ("Could not allocate vertices")
        // and to ensure we don't accidentally fill the checkerboard pattern.
        using var sourceBitmap = GetFlattenedContentBitmap();
        var targetColor = sourceBitmap.GetPixel(x, y);

        // Don't fill if clicking on the same color (with some tolerance for anti-aliasing)
        if (ColorsAreSimilar(targetColor, fillColor, tolerance: 30))
            return null;

        // Create a bitmap and surface for drawing the fill result
        var fillBitmap = new SKBitmap(
            CanvasSize.Width,
            CanvasSize.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var surface = SKSurface.Create(
            new SKImageInfo(CanvasSize.Width, CanvasSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul)
        );
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Perform flood fill and draw horizontal spans
        var hasContent = ScanlineFillWithCanvas(sourceBitmap, canvas, x, y, targetColor, fillColor);

        if (!hasContent)
        {
            fillBitmap.Dispose();
            return null;
        }

        // Copy the surface to the bitmap
        canvas.Flush();
        using var filledImage = surface.Snapshot();

        // Create a new bitmap with the filled content
        var resultBitmap = new SKBitmap(
            CanvasSize.Width,
            CanvasSize.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var resultCanvas = new SKCanvas(resultBitmap);
        resultCanvas.DrawImage(filledImage, 0, 0);
        resultCanvas.Flush();

        // Create a bitmap path with the fill result
        var fillPath = new PenPath
        {
            PathType = PenPathType.Bitmap,
            FillColor = fillColor,
            BitmapData = resultBitmap,
            Bounds = new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height),
        };

        Paths = Paths.Add(fillPath);
        ClearRedoStack(); // New path added, clear redo history
        InvalidatePathCache();
        RefreshCanvas?.Invoke();

        return fillPath;
    }

    /// <summary>
    /// Generates a flattened bitmap of the current canvas content (Layers + Paths).
    /// Runs strictly on CPU to avoid GPU threading/context issues during Flood Fill.
    /// Ignores checkerboard background to ensure correct filling of transparent areas.
    /// </summary>
    private SKBitmap GetFlattenedContentBitmap()
    {
        var width = (int)CanvasSize.Width;
        var height = (int)CanvasSize.Height;
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Draw all layers in order
        foreach (var (name, layer) in Layers)
        {
            lock (layer)
            {
                foreach (var layerBitmap in layer.Bitmaps)
                {
                    canvas.DrawBitmap(layerBitmap, 0, 0);
                }

                // If this is the active brush layer, also render the active vector paths
                // We render them freshly here on CPU to avoid using the GPU-backed cache from a different thread
                if (name == "Brush")
                {
                    using var paint = new SKPaint();
                    foreach (var path in Paths)
                    {
                        RenderPenPath(canvas, path, paint);
                    }
                }
            }
        }

        canvas.Flush();
        return bitmap;
    }

    /// <summary>
    /// Scanline flood fill that draws horizontal spans to an SKCanvas.
    /// Returns true if any pixels were filled.
    /// </summary>
    private static bool ScanlineFillWithCanvas(
        SKBitmap source,
        SKCanvas canvas,
        int startX,
        int startY,
        SKColor targetColor,
        SKColor fillColor
    )
    {
        var width = source.Width;
        var height = source.Height;

        // Use SKBitmap.Pixels which is platform-agnostic (returns SKColor[])
        var sourcePixels = source.Pixels;

        var visited = new bool[width * height];
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));

        // Collect horizontal spans to draw
        var spans = new List<(int y, int left, int right)>();

        // Increased tolerance to better catch anti-aliased edges
        const int Tolerance = 50;
        // Increased expansion to ensuring we fully cover the semi-transparent border pixels
        const float Expand = 1.5f;

        using var paint = new SKPaint
        {
            Color = fillColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true, // Smooth edges for the dilated rects
            BlendMode = SKBlendMode.Src, // Replace mode prevents alpha buildup on overlapping dilated scanlines
        };

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            // Bounds check
            if (x < 0 || x >= width || y < 0 || y >= height)
                continue;

            var index = y * width + x;
            if (visited[index])
                continue;

            var pixel = sourcePixels[index];
            if (!ColorsAreSimilar(pixel, targetColor, tolerance: Tolerance))
                continue;

            // Mark as visited
            visited[index] = true;

            // Scanline approach: find the entire horizontal span
            var left = x;
            var right = x;

            // Extend left
            while (left > 0)
            {
                var leftIndex = y * width + (left - 1);
                if (visited[leftIndex])
                    break;
                var leftPixel = sourcePixels[leftIndex];
                if (!ColorsAreSimilar(leftPixel, targetColor, tolerance: Tolerance))
                    break;
                left--;
                visited[leftIndex] = true;
            }

            // Extend right
            while (right < width - 1)
            {
                var rightIndex = y * width + (right + 1);
                if (visited[rightIndex])
                    break;
                var rightPixel = sourcePixels[rightIndex];
                if (!ColorsAreSimilar(rightPixel, targetColor, tolerance: Tolerance))
                    break;
                right++;
                visited[rightIndex] = true;
            }

            // Draw this span as a filled rectangle with slight expansion
            // Using DrawRect with float coordinates allows sub-pixel expansion
            canvas.DrawRect(
                left - Expand,
                y - Expand,
                (right - left + 1) + (Expand * 2),
                1 + (Expand * 2),
                paint
            );

            // Queue pixels above and below the span
            for (var i = left; i <= right; i++)
            {
                if (y > 0)
                {
                    var aboveIndex = (y - 1) * width + i;
                    if (!visited[aboveIndex])
                    {
                        var abovePixel = sourcePixels[aboveIndex];
                        if (ColorsAreSimilar(abovePixel, targetColor, tolerance: Tolerance))
                            queue.Enqueue((i, y - 1));
                    }
                }

                if (y < height - 1)
                {
                    var belowIndex = (y + 1) * width + i;
                    if (!visited[belowIndex])
                    {
                        var belowPixel = sourcePixels[belowIndex];
                        if (ColorsAreSimilar(belowPixel, targetColor, tolerance: Tolerance))
                            queue.Enqueue((i, y + 1));
                    }
                }
            }
        }

        // Check if anything was filled (at least one visited pixel)
        foreach (var v in visited)
        {
            if (v)
                return true;
        }

        return false;
    }

    private static bool ColorsAreSimilar(SKColor a, SKColor b, int tolerance)
    {
        // Handle transparent pixels specially
        if (a.Alpha < 10 && b.Alpha < 10)
            return true;
        if (a.Alpha < 10 || b.Alpha < 10)
            return Math.Abs(a.Alpha - b.Alpha) <= tolerance;

        return Math.Abs(a.Red - b.Red) <= tolerance
            && Math.Abs(a.Green - b.Green) <= tolerance
            && Math.Abs(a.Blue - b.Blue) <= tolerance
            && Math.Abs(a.Alpha - b.Alpha) <= tolerance;
    }

    #endregion

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
        var grContext = surface.Context;
        var useGpu = UseGpuAcceleration && grContext != null;
        IsUsingGpu = useGpu;

        // Store the context for use in cache creation
        currentGrContext = grContext;

        // Initialize canvas layers
        foreach (var layer in Layers.Values)
        {
            lock (layer)
            {
                var needsNewSurface = layer.Surface is null;
                if (!needsNewSurface)
                {
                    // Check if we need to resize
                    var currentInfo = layer.Surface!.Canvas.DeviceClipBounds;
                    needsNewSurface =
                        currentInfo.Width != CanvasSize.Width || currentInfo.Height != CanvasSize.Height;
                }

                if (needsNewSurface)
                {
                    // Dispose old surface if exists
                    layer.Surface?.Dispose();

                    var imageInfo = new SKImageInfo(CanvasSize.Width, CanvasSize.Height);

                    // Try GPU surface first if available
                    if (useGpu)
                    {
                        layer.Surface = SKSurface.Create(grContext!, budgeted: true, imageInfo);

                        // Fallback to CPU if GPU surface creation failed
                        if (layer.Surface is null)
                        {
                            if (LogRenderingMode)
                            {
                                logger.LogWarning(
                                    "GPU surface creation failed, falling back to CPU for layer"
                                );
                            }
                            layer.Surface = SKSurface.Create(imageInfo);
                        }
                        else if (LogRenderingMode)
                        {
                            logger.LogDebug("Created GPU-accelerated surface for layer");
                        }
                    }
                    else
                    {
                        layer.Surface = SKSurface.Create(imageInfo);
                        if (LogRenderingMode)
                        {
                            logger.LogDebug("Created CPU surface for layer (GPU not available or disabled)");
                        }
                    }
                }
                else
                {
                    // No resize needed, just clear
                    layer.Surface!.Canvas.Clear(SKColors.Transparent);
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

        // Render paint layer with caching optimization
        RenderPathsWithCaching(BrushLayer.Surface!.Canvas);

        // Draw background - either checkerboard for transparency or clear
        // Draw background - either checkerboard for transparency or clear
        // Include check for renderBackgroundFill so snapshots (like FloodFill analysis) can skip the checkerboard pattern
        if (ShowCheckerboardBackground && renderBackgroundFill)
        {
            RenderCheckerboardBackground(surface.Canvas);
        }
        else
        {
            surface.Canvas.Clear(SKColors.Transparent);
        }

        // Draw the layers to the main surface
        foreach (var layer in Layers.Values)
        {
            lock (layer)
            {
                layer.Surface!.Canvas.Flush();
                surface.Canvas.DrawSurface(layer.Surface!, new SKPoint(0, 0));
            }
        }

        // Draw grid overlay if enabled
        if (ShowGridOverlay)
        {
            RenderGridOverlay(surface.Canvas);
        }

        surface.Canvas.Flush();
    }

    /// <summary>
    /// Renders a checkerboard pattern to indicate transparent areas.
    /// Uses a cached shader for efficient repeated rendering.
    /// </summary>
    private void RenderCheckerboardBackground(SKCanvas canvas)
    {
        // Check if we need to create or recreate the shader
        if (cachedCheckerboardShader is null || cachedCheckerboardSize != CanvasSize)
        {
            cachedCheckerboardShader?.Dispose();
            cachedCheckerboardShader = CreateCheckerboardShader();
            cachedCheckerboardSize = CanvasSize;
        }

        using var paint = new SKPaint { Shader = cachedCheckerboardShader, IsAntialias = false };

        canvas.DrawRect(0, 0, CanvasSize.Width, CanvasSize.Height, paint);
    }

    /// <summary>
    /// Creates a checkerboard pattern shader using a small tiled bitmap.
    /// </summary>
    private static SKShader CreateCheckerboardShader()
    {
        // Create a small 2x2 checker bitmap (in units of square size)
        var tileSize = CheckerboardSquareSize * 2;
        using var tileBitmap = new SKBitmap(tileSize, tileSize);
        using var tileCanvas = new SKCanvas(tileBitmap);

        // Draw the four squares
        using var lightPaint = new SKPaint { Color = CheckerboardLight };
        using var darkPaint = new SKPaint { Color = CheckerboardDark };

        // Top-left and bottom-right are light
        tileCanvas.DrawRect(0, 0, CheckerboardSquareSize, CheckerboardSquareSize, lightPaint);
        tileCanvas.DrawRect(
            CheckerboardSquareSize,
            CheckerboardSquareSize,
            CheckerboardSquareSize,
            CheckerboardSquareSize,
            lightPaint
        );

        // Top-right and bottom-left are dark
        tileCanvas.DrawRect(
            CheckerboardSquareSize,
            0,
            CheckerboardSquareSize,
            CheckerboardSquareSize,
            darkPaint
        );
        tileCanvas.DrawRect(
            0,
            CheckerboardSquareSize,
            CheckerboardSquareSize,
            CheckerboardSquareSize,
            darkPaint
        );

        tileCanvas.Flush();

        // Create a shader that tiles this bitmap
        return SKShader.CreateBitmap(tileBitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
    }

    /// <summary>
    /// Renders a grid overlay for alignment assistance (e.g., rule of thirds).
    /// </summary>
    private void RenderGridOverlay(SKCanvas canvas)
    {
        if (GridDivisions <= 1 || CanvasSize == Size.Empty)
            return;

        using var paint = new SKPaint
        {
            Color = GridLineColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
        };

        var width = CanvasSize.Width;
        var height = CanvasSize.Height;

        // Draw vertical lines
        for (var i = 1; i < GridDivisions; i++)
        {
            var x = (float)(width * i) / GridDivisions;
            canvas.DrawLine(x, 0, x, height, paint);
        }

        // Draw horizontal lines
        for (var i = 1; i < GridDivisions; i++)
        {
            var y = (float)(height * i) / GridDivisions;
            canvas.DrawLine(0, y, width, y, paint);
        }
    }

    /// <summary>
    /// Renders paths with caching optimization. Completed paths are cached
    /// to avoid re-rendering them every frame.
    /// </summary>
    private void RenderPathsWithCaching(SKCanvas paintLayerCanvas)
    {
        var currentPathCount = Paths.Count;
        var hasTemporaryPaths = !TemporaryPaths.IsEmpty;

        // Check if we can use the cached image
        if (cachedPathsImage != null && cachedPathsCount == currentPathCount && !hasTemporaryPaths)
        {
            // All paths are cached and no temporary paths - just draw the cached image
            paintLayerCanvas.DrawImage(cachedPathsImage, new SKPoint(0, 0));
            return;
        }

        // Check if we need to update the cache (new completed paths)
        if (cachedPathsCount < currentPathCount && !hasTemporaryPaths)
        {
            // Render all completed paths to a new cached image
            UpdatePathCache();

            if (cachedPathsImage != null)
            {
                paintLayerCanvas.DrawImage(cachedPathsImage, new SKPoint(0, 0));
                return;
            }
        }

        // Fallback: render with partial caching
        using var paint = new SKPaint();

        // If we have a cache, draw it first
        if (cachedPathsImage != null && cachedPathsCount > 0)
        {
            paintLayerCanvas.DrawImage(cachedPathsImage, new SKPoint(0, 0));

            // Only render paths that aren't in the cache
            for (var i = cachedPathsCount; i < currentPathCount; i++)
            {
                RenderPenPath(paintLayerCanvas, Paths[i], paint);
            }
        }
        else
        {
            // No cache, render all paths
            foreach (var penPath in Paths)
            {
                RenderPenPath(paintLayerCanvas, penPath, paint);
            }
        }

        // Render temporary paths directly (the batched RenderPenPath is already optimized)
        foreach (var penPath in TemporaryPaths.Values)
        {
            RenderPenPath(paintLayerCanvas, penPath, paint);
        }
    }

    /// <summary>
    /// Renders temporary paths with incremental caching for long strokes.
    /// Only new points since last render are drawn, dramatically improving
    /// performance for continuous drawing.
    /// </summary>
    private void RenderTemporaryPathsIncremental(SKCanvas targetCanvas, SKPaint paint)
    {
        if (TemporaryPaths.IsEmpty)
        {
            // No temporary paths - dispose surface if exists
            if (tempPathSurface != null)
            {
                tempPathSurface.Dispose();
                tempPathSurface = null;
                tempPathRenderedPoints.Clear();
            }
            return;
        }

        // For simplicity and reliability, use a hybrid approach:
        // - Keep a cached surface for the "already rendered" portions
        // - Render new points directly to target canvas (which gets composited)

        // Ensure we have a temp surface
        var needNewSurface = tempPathSurface == null;
        if (!needNewSurface)
        {
            var bounds = tempPathSurface!.Canvas.DeviceClipBounds;
            needNewSurface = bounds.Width != CanvasSize.Width || bounds.Height != CanvasSize.Height;
        }

        if (needNewSurface)
        {
            tempPathSurface?.Dispose();
            var imageInfo = new SKImageInfo(CanvasSize.Width, CanvasSize.Height);

            // Try GPU surface first
            if (IsUsingGpu && currentGrContext != null)
            {
                tempPathSurface = SKSurface.Create(currentGrContext, budgeted: true, imageInfo);
            }
            tempPathSurface ??= SKSurface.Create(imageInfo);
            tempPathSurface?.Canvas.Clear(SKColors.Transparent);
            tempPathRenderedPoints.Clear();
        }

        if (tempPathSurface == null)
        {
            // Fallback: render all temp paths directly
            foreach (var penPath in TemporaryPaths.Values)
            {
                RenderPenPath(targetCanvas, penPath, paint);
            }
            return;
        }

        var tempCanvas = tempPathSurface.Canvas;

        // Check if any paths were removed (stroke finalized) - need to clear and rebuild
        var pathsRemoved = false;
        foreach (var pointerId in tempPathRenderedPoints.Keys.ToArray())
        {
            if (!TemporaryPaths.ContainsKey(pointerId))
            {
                pathsRemoved = true;
                tempPathRenderedPoints.TryRemove(pointerId, out _);
            }
        }

        if (pathsRemoved)
        {
            // A stroke was finalized - clear the temp surface
            tempCanvas.Clear(SKColors.Transparent);
            tempPathRenderedPoints.Clear();
        }

        // Render each temporary path
        foreach (var (pointerId, penPath) in TemporaryPaths)
        {
            var renderedCount = tempPathRenderedPoints.GetValueOrDefault(pointerId, 0);
            var totalPoints = penPath.Points.Count;

            if (totalPoints > renderedCount)
            {
                if (renderedCount == 0)
                {
                    // New path - render everything to the temp surface
                    RenderPenPath(tempCanvas, penPath, paint);
                }
                else
                {
                    // Continuing path - render new segment to temp surface
                    RenderPenPathSegment(tempCanvas, penPath, renderedCount, totalPoints, paint);
                }
                tempPathRenderedPoints[pointerId] = totalPoints;
            }
        }

        // Draw the temp surface to target
        tempCanvas.Flush();
        using var tempImage = tempPathSurface.Snapshot();
        targetCanvas.DrawImage(tempImage, new SKPoint(0, 0));
    }

    /// <summary>
    /// Renders a segment of a pen path (from startIndex to endIndex).
    /// Used for incremental rendering of temporary paths.
    /// </summary>
    private static void RenderPenPathSegment(
        SKCanvas canvas,
        PenPath penPath,
        int startIndex,
        int endIndex,
        SKPaint paint
    )
    {
        if (startIndex >= endIndex || penPath.Points.Count == 0)
            return;

        // Apply Color
        if (penPath.IsErase)
        {
            paint.BlendMode = SKBlendMode.Clear;
            paint.Color = SKColors.Transparent;
        }
        else
        {
            paint.BlendMode = SKBlendMode.SrcOver;
            paint.Color = penPath.FillColor;
        }

        paint.IsDither = true;
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;

        using var path = new SKPath();
        var started = false;
        var currentThickness = 0f;
        var prevX = 0f;
        var prevY = 0f;

        // Start from one point before to ensure continuity
        var actualStart = Math.Max(0, startIndex - 1);

        for (var i = actualStart; i < endIndex && i < penPath.Points.Count; i++)
        {
            var point = penPath.Points[i];
            if (!point.IsPen)
                continue;

            var thickness = (float)((point.Pressure ?? 1) * point.Radius * 2.5);

            if (!started)
            {
                path.MoveTo(point.X, point.Y);
                currentThickness = thickness;
                started = true;
            }
            else
            {
                path.LineTo(point.X, point.Y);
                currentThickness = (currentThickness + thickness) / 2;
            }

            prevX = point.X;
            prevY = point.Y;
        }

        if (started)
        {
            paint.StrokeWidth = currentThickness;
            canvas.DrawPath(path, paint);
        }
    }

    /// <summary>
    /// Clears the temporary path cache. Call when a stroke is finalized.
    /// </summary>
    public void ClearTempPathCache()
    {
        tempPathSurface?.Dispose();
        tempPathSurface = null;
        tempPathRenderedPoints.Clear();
    }

    /// <summary>
    /// Updates the path cache with all current completed paths.
    /// Uses GPU-backed surface if GPU acceleration is active.
    /// </summary>
    private void UpdatePathCache()
    {
        if (CanvasSize == Size.Empty || Paths.Count == 0)
        {
            cachedPathsImage?.Dispose();
            cachedPathsImage = null;
            cachedPathsCount = 0;
            return;
        }

        var imageInfo = new SKImageInfo(CanvasSize.Width, CanvasSize.Height);
        SKSurface? cacheSurface = null;

        // Try to create GPU-backed surface if GPU is active
        if (IsUsingGpu && currentGrContext != null)
        {
            try
            {
                cacheSurface = SKSurface.Create(currentGrContext, budgeted: true, imageInfo);
                if (cacheSurface != null && LogRenderingMode)
                {
                    logger.LogDebug("Created GPU-backed cache surface");
                }
            }
            catch (Exception ex)
            {
                if (LogRenderingMode)
                {
                    logger.LogWarning(ex, "Failed to create GPU cache surface, falling back to CPU");
                }
            }
        }

        // Fallback to CPU surface if GPU failed or not available
        if (cacheSurface == null)
        {
            cacheSurface = SKSurface.Create(imageInfo);
            if (LogRenderingMode && IsUsingGpu)
            {
                logger.LogDebug("Created CPU cache surface (GPU context was unavailable)");
            }
        }

        if (cacheSurface == null)
        {
            logger.LogWarning("Failed to create any cache surface");
            return;
        }

        using (cacheSurface)
        {
            var cacheCanvas = cacheSurface.Canvas;
            cacheCanvas.Clear(SKColors.Transparent);

            using var paint = new SKPaint();

            // Render all completed paths
            foreach (var penPath in Paths)
            {
                RenderPenPath(cacheCanvas, penPath, paint);
            }

            // Save the cached image
            cachedPathsImage?.Dispose();
            cachedPathsImage = cacheSurface.Snapshot();
            cachedPathsCount = Paths.Count;

            if (LogRenderingMode)
            {
                logger.LogDebug("Updated path cache with {Count} paths", cachedPathsCount);
            }
        }
    }

    /// <summary>
    /// Renders a pen path to a canvas. This method is public so it can be shared
    /// with other ViewModels like LayeredMaskEditorViewModel.
    /// Optimized to batch draw calls into a single SKPath for performance.
    /// </summary>
    /// <param name="overrideColor">If provided, uses this color instead of the path's FillColor. Useful for mask export.</param>
    public static void RenderPenPath(
        SKCanvas canvas,
        PenPath penPath,
        SKPaint paint,
        SKColor? overrideColor = null
    )
    {
        // Apply Color and blend mode
        if (penPath.IsErase)
        {
            paint.BlendMode = SKBlendMode.Clear;
            paint.Color = SKColors.Transparent;
        }
        else
        {
            paint.BlendMode = SKBlendMode.SrcOver;
            paint.Color = overrideColor ?? penPath.FillColor;
        }

        paint.IsDither = true;
        paint.IsAntialias = true;

        // Handle shape path types (Rectangle, Ellipse, Bitmap)
        switch (penPath.PathType)
        {
            case PenPathType.Rectangle:
                if (penPath.IsStrokeOnly)
                {
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = penPath.StrokeWidth;
                }
                else
                {
                    paint.Style = SKPaintStyle.Fill;
                }
                canvas.DrawRect(penPath.Bounds, paint);
                return;

            case PenPathType.Ellipse:
                if (penPath.IsStrokeOnly)
                {
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = penPath.StrokeWidth;
                }
                else
                {
                    paint.Style = SKPaintStyle.Fill;
                }
                canvas.DrawOval(penPath.Bounds, paint);
                return;

            case PenPathType.Bitmap:
                if (penPath.BitmapData != null)
                {
                    if (overrideColor.HasValue)
                    {
                        // Apply color filter to replace colors with override while keeping alpha
                        var color = overrideColor.Value;
                        using var colorPaint = new SKPaint();
                        // Color matrix that replaces RGB with override color, preserves alpha
                        // csharpier-ignore
                        colorPaint.ColorFilter = SKColorFilter.CreateColorMatrix(
                        [
                            0, 0, 0, 0, color.Red / 255f,
                            0, 0, 0, 0, color.Green / 255f,
                            0, 0, 0, 0, color.Blue / 255f,
                            0, 0, 0, 1, 0
                        ]);
                        canvas.DrawBitmap(
                            penPath.BitmapData,
                            penPath.Bounds.Left,
                            penPath.Bounds.Top,
                            colorPaint
                        );
                    }
                    else
                    {
                        canvas.DrawBitmap(penPath.BitmapData, penPath.Bounds.Left, penPath.Bounds.Top);
                    }
                }
                return;

            case PenPathType.Freehand:
            default:
                // Continue with freehand rendering below
                break;
        }

        // Freehand path rendering
        if (penPath.Points.Count == 0)
        {
            return;
        }

        // Apply Color
        if (penPath.IsErase)
        {
            paint.BlendMode = SKBlendMode.Clear;
            paint.Color = SKColors.Transparent;
        }
        else
        {
            paint.BlendMode = SKBlendMode.SrcOver;
            paint.Color = overrideColor ?? penPath.FillColor;
        }

        // Setup paint for strokes
        paint.IsDither = true;
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round; // Round caps handle endpoints
        paint.StrokeJoin = SKStrokeJoin.Round;

        // Count pen points and check pressure uniformity in a single pass (avoids LINQ allocations)
        var penPointCount = 0;
        var uniformPressure = true;
        var firstPressure = 0.0;
        var totalThickness = 0.0;
        var firstPenPointIndex = -1;

        for (var i = 0; i < penPath.Points.Count; i++)
        {
            var p = penPath.Points[i];
            if (!p.IsPen)
                continue;

            var pressure = p.Pressure ?? 1;
            var thickness = pressure * p.Radius * 2.5;

            if (penPointCount == 0)
            {
                firstPressure = pressure;
                firstPenPointIndex = i;
            }
            else if (uniformPressure && Math.Abs(pressure - firstPressure) >= 0.1)
            {
                uniformPressure = false;
            }

            totalThickness += thickness;
            penPointCount++;
        }

        if (penPointCount == 0)
        {
            // No pen points - use the ToSKPath method for mouse-based paths
            var point = penPath.Points[0];
            paint.StrokeWidth = (float)(point.Radius * 2);
            var skPath = penPath.ToSKPath();
            canvas.DrawPath(skPath, paint);
            return;
        }

        // For pressure-sensitive drawing, we need to handle variable thickness
        if (penPointCount == 1)
        {
            // Single point - draw a circle
            var point = penPath.Points[firstPenPointIndex];
            var thickness = (point.Pressure ?? 1) * point.Radius * 2.5;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(point.X, point.Y, (float)(thickness / 2), paint);
            return;
        }

        if (uniformPressure)
        {
            // All points have similar pressure - batch into single path
            var avgThickness = totalThickness / penPointCount;
            paint.StrokeWidth = (float)avgThickness;

            using var path = new SKPath();
            var started = false;

            for (var i = 0; i < penPath.Points.Count; i++)
            {
                var p = penPath.Points[i];
                if (!p.IsPen)
                    continue;

                if (!started)
                {
                    path.MoveTo(p.X, p.Y);
                    started = true;
                }
                else
                {
                    path.LineTo(p.X, p.Y);
                }
            }

            canvas.DrawPath(path, paint);
        }
        else
        {
            // Variable pressure - draw segments with varying thickness
            // Batch into groups of similar thickness for fewer draw calls
            using var path = new SKPath();
            var currentThickness = 0f;
            var pathStarted = false;
            var lastPenX = 0f;
            var lastPenY = 0f;

            for (var i = 0; i < penPath.Points.Count; i++)
            {
                var point = penPath.Points[i];
                if (!point.IsPen)
                    continue;

                var thickness = (float)((point.Pressure ?? 1) * point.Radius * 2.5);

                // If thickness changed significantly, draw current path and start new one
                if (pathStarted && Math.Abs(thickness - currentThickness) > currentThickness * 0.2f)
                {
                    paint.StrokeWidth = currentThickness;
                    canvas.DrawPath(path, paint);
                    path.Reset();

                    // Start new path from previous point for continuity
                    path.MoveTo(lastPenX, lastPenY);
                    pathStarted = false;
                }

                if (!pathStarted)
                {
                    path.MoveTo(point.X, point.Y);
                    currentThickness = thickness;
                    pathStarted = true;
                }
                else
                {
                    path.LineTo(point.X, point.Y);
                    // Smoothly blend thickness
                    currentThickness = (currentThickness + thickness) / 2;
                }

                lastPenX = point.X;
                lastPenY = point.Y;
            }

            // Draw remaining path
            if (pathStarted)
            {
                paint.StrokeWidth = currentThickness;
                canvas.DrawPath(path, paint);
            }
        }
    }
}
