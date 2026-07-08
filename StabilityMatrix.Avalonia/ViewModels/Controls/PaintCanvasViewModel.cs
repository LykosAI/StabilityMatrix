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
public partial class PaintCanvasViewModel(ILogger<PaintCanvasViewModel> logger)
    : LoadableViewModelBase,
        IDisposable
{
    // Threading model (no locks):
    //  * UI thread: all mutation (strokes, undo/redo, layer bitmap swaps, exports, Dispose).
    //    Shared state crosses to the render thread only as immutable snapshots — ImmutableList
    //    swaps for Paths and SKLayer.Bitmaps, LiveStroke point-array publication.
    //  * Render thread (compositor): RenderToSurface only. It exclusively owns the persistent
    //    native objects (SKLayer.Surface, cachedPathsImage, the checkerboard shader) — they are
    //    created, rebuilt and disposed only inside the render pass.
    //  * Cross-thread disposal is deferred: bitmaps swapped out on the UI thread are queued to
    //    retiredLayerBitmaps and freed by the render thread after the frame completes; cache
    //    invalidation sets pathCacheDirty instead of disposing.
    //  * Dispose quiesces first: it sets _disposed (checked at render entry) and waits for
    //    rendersInFlight to drain before freeing render-owned resources.
    private volatile bool _disposed;

    /// <summary>
    /// Number of render passes currently inside <see cref="RenderToSurface"/>. Used by
    /// <see cref="Dispose"/> to wait for the in-flight frame before freeing native resources.
    /// </summary>
    private int rendersInFlight;

    /// <summary>
    /// Set (to 1) by the UI thread when the finalized-path cache is stale; the render thread
    /// consumes it with an atomic read-and-reset and disposes/rebuilds
    /// <see cref="cachedPathsImage"/> on its own thread. Int rather than bool so
    /// <see cref="Interlocked.Exchange(ref int, int)"/> can swap it — a plain check-then-clear
    /// would drop an invalidation raised between the check and the clear.
    /// </summary>
    private int pathCacheDirty;

    /// <summary>
    /// Bitmaps swapped out of layers on the UI thread while the render thread may still be
    /// drawing them. Drained (disposed) by the render thread after each frame, and by
    /// <see cref="Dispose"/>.
    /// </summary>
    private readonly ConcurrentQueue<SKBitmap> retiredLayerBitmaps = new();

    /// <summary>
    /// Strokes currently being drawn, keyed by pointer id. Values are <see cref="LiveStroke"/>s:
    /// the UI thread appends points while the render thread reads stable snapshots, without locks.
    /// </summary>
    public ConcurrentDictionary<long, LiveStroke> TemporaryPaths { get; set; } = new();

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

    /// <summary>
    /// Feathering amount for soft brush edges. 0 = hard edge, 1 = fully soft/blurred.
    /// UI typically shows this inverted as "Hardness" (100% = no feathering).
    /// </summary>
    [ObservableProperty]
    private double paintBrushFeathering = 0;

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

        if (!Layers.TryGetValue(layerName, out var layer))
        {
            return;
        }

        var oldBitmaps = layer.Bitmaps;
        layer.Bitmaps = bitmap is not null ? [bitmap] : [];
        RetireLayerBitmaps(oldBitmaps);
    }

    public void LoadCanvasFromBitmap(SKBitmap bitmap)
    {
        var oldBitmaps = ImagesLayer.Bitmaps;
        ImagesLayer.Bitmaps = [bitmap];
        RetireLayerBitmaps(oldBitmaps);

        InvalidatePathCache();
        RefreshCanvas?.Invoke();
    }

    /// <summary>
    /// Frees bitmaps that were swapped out of a layer. When the canvas renders on-screen
    /// (<see cref="RefreshCanvas"/> is wired), the render thread may still be drawing them,
    /// so they are queued and disposed by the render thread after the frame. For export-only
    /// view models that never render on-screen, they are disposed immediately.
    /// </summary>
    private void RetireLayerBitmaps(ImmutableList<SKBitmap> oldBitmaps)
    {
        foreach (var oldBitmap in oldBitmaps)
        {
            if (RefreshCanvas is null)
            {
                oldBitmap.Dispose();
            }
            else
            {
                retiredLayerBitmaps.Enqueue(oldBitmap);
            }
        }
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
    /// The cache itself is owned by the render thread, so this only raises a flag; the
    /// render thread disposes and rebuilds the cache on its own thread.
    /// </summary>
    public void InvalidatePathCache()
    {
        Interlocked.Exchange(ref pathCacheDirty, 1);
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

    #region Move Tool State

    /// <summary>
    /// Starting point for move operations.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private SKPoint? moveStartPoint;

    /// <summary>
    /// Layer offset at the start of a move operation.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private SKPoint moveStartOffset;

    /// <summary>
    /// Returns true if the currently selected tool is the move tool.
    /// </summary>
    [JsonIgnore]
    public bool IsMoveTool => SelectedTool == PaintCanvasTool.Move;

    /// <summary>
    /// Callback invoked when the move tool adjusts the image layer position.
    /// Parameters: (newOffsetX, newOffsetY) - the new absolute offset position.
    /// </summary>
    [JsonIgnore]
    public Action<double, double>? OnMoveToolDrag { get; set; }

    /// <summary>
    /// Callback to get the current image layer offset when starting a move.
    /// Returns (currentOffsetX, currentOffsetY).
    /// </summary>
    [JsonIgnore]
    public Func<(double X, double Y)>? GetCurrentMoveOffset { get; set; }

    /// <summary>
    /// Starts a move operation at the given position.
    /// </summary>
    public void StartMove(SKPoint position, double currentOffsetX, double currentOffsetY)
    {
        MoveStartPoint = position;
        MoveStartOffset = new SKPoint((float)currentOffsetX, (float)currentOffsetY);
    }

    /// <summary>
    /// Updates the move during drag, calculating delta from start position.
    /// </summary>
    public void UpdateMove(SKPoint currentPoint)
    {
        if (!MoveStartPoint.HasValue)
            return;

        var deltaX = currentPoint.X - MoveStartPoint.Value.X;
        var deltaY = currentPoint.Y - MoveStartPoint.Value.Y;

        // Invoke callback with new absolute offset
        OnMoveToolDrag?.Invoke(MoveStartOffset.X + deltaX, MoveStartOffset.Y + deltaY);
    }

    /// <summary>
    /// Ends the current move operation.
    /// </summary>
    public void EndMove()
    {
        MoveStartPoint = null;
    }

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

    [RelayCommand]
    public void SelectMoveTool() => SelectedTool = PaintCanvasTool.Move;

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
        TemporaryPaths[ShapePointerId] = new LiveStroke { Template = previewPath };
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

        // Create a surface for drawing the fill result
        using var surface = SKSurface.Create(
            new SKImageInfo(CanvasSize.Width, CanvasSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul)
        );
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Perform flood fill and draw horizontal spans
        var hasContent = ScanlineFillWithCanvas(sourceBitmap, canvas, x, y, targetColor, fillColor);

        if (!hasContent)
        {
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
        var width = CanvasSize.Width;
        var height = CanvasSize.Height;
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Draw all layers in order. Runs on the UI thread; layer.Bitmaps is an atomically-swapped
        // immutable list only ever mutated from the UI thread, so a plain read is safe.
        foreach (var (name, layer) in Layers)
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

        var hasContent = false;

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
            hasContent = true;

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

        return hasContent;
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

        using var originalImage = ComposeToNewImage(renderBackgroundImage: false);
        if (originalImage is null)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(originalImage.Width, originalImage.Height));
        if (surface is null)
        {
            logger.LogWarning("RenderToWhiteChannelImage: Failed to create surface, returning null.");
            return null;
        }
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

    /// <summary>
    /// Composes the canvas into a new CPU-backed image on the calling (UI) thread, without
    /// touching the persistent surfaces owned by the on-screen render pass.
    /// </summary>
    /// <param name="renderBackgroundImage">Whether to include the background image layer.</param>
    public SKImage? RenderToImage(bool renderBackgroundImage = false)
    {
        using var _ = CodeTimer.StartDebug();

        return ComposeToNewImage(renderBackgroundImage);
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

        // Use flat arrays in the per-pixel loop to avoid dictionary lookups per pixel per color.
        // default(SKColor) is transparent, so only matches need to be written.
        var colorCount = targetColors.Count;
        var colors = new SKColor[colorCount];
        var resultPixels = new SKColor[colorCount][];
        for (var c = 0; c < colorCount; c++)
        {
            colors[c] = targetColors[c];
            resultPixels[c] = new SKColor[pixelCount];
        }

        // Single pass through pixels, check all colors
        for (var i = 0; i < pixelCount; i++)
        {
            var pixel = srcPixels[i];

            if (pixel.Alpha == 0)
                continue;

            for (var c = 0; c < colorCount; c++)
            {
                var targetColor = colors[c];
                if (
                    Math.Abs(pixel.Red - targetColor.Red) <= tolerance
                    && Math.Abs(pixel.Green - targetColor.Green) <= tolerance
                    && Math.Abs(pixel.Blue - targetColor.Blue) <= tolerance
                )
                {
                    resultPixels[c][i] = SKColors.White;
                }
            }
        }

        // Set pixels and convert bitmaps to images
        for (var c = 0; c < colorCount; c++)
        {
            using var bitmap = new SKBitmap(
                sourceBitmap.Width,
                sourceBitmap.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul
            );
            bitmap.Pixels = resultPixels[c];
            results[colors[c]] = SKImage.FromBitmap(bitmap);
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

        foreach (var pixel in pixels)
        {
            if (pixel.Alpha < 128) // Skip mostly transparent pixels
                continue;

            // Find the closest palette color
            for (var p = 0; p < paletteCount; p++)
            {
                var paletteColor = paletteColors[p];
                if (!ColorMatchesWithTolerance(pixel, paletteColor, tolerance))
                    continue;

                foundPaletteColors.Add(paletteColor);

                // Early exit if we've found all palette colors
                if (foundPaletteColors.Count == paletteCount)
                    return foundPaletteColors.ToList();

                break;
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

    /// <summary>
    /// On-screen render entry point, called by the compositor render thread each frame.
    /// Tracked by <see cref="rendersInFlight"/> so <see cref="Dispose"/> can wait for the
    /// in-flight frame before freeing the native resources this pass draws with.
    /// </summary>
    public void RenderToSurface(
        SKSurface surface,
        bool renderBackgroundFill = false,
        bool renderBackgroundImage = false
    )
    {
        Interlocked.Increment(ref rendersInFlight);
        try
        {
            if (_disposed)
            {
                return;
            }

            RenderToSurfaceCore(surface, renderBackgroundFill, renderBackgroundImage);
        }
        finally
        {
            Interlocked.Decrement(ref rendersInFlight);
        }
    }

    private void RenderToSurfaceCore(
        SKSurface? surface,
        bool renderBackgroundFill,
        bool renderBackgroundImage
    )
    {
        // SKSurface.Create can return null under low memory or GPU context loss
        if (surface is null || _disposed)
        {
            return;
        }

        // A zero-size canvas would make SKSurface.Create return null below and NRE on layer.Surface
        if (CanvasSize.Width <= 0 || CanvasSize.Height <= 0)
        {
            surface.Canvas.Clear(SKColors.Transparent);
            return;
        }

        var grContext = surface.Context;
        var useGpu = UseGpuAcceleration && grContext != null;
        IsUsingGpu = useGpu;

        // Initialize canvas layers. The persistent layer surfaces are exclusively owned by this
        // render pass (exports compose their own CPU surfaces from immutable snapshots — see
        // PaintCanvasViewModel.Compose.cs), so no locking is needed. Recreate when missing, when
        // the GPU context changed (device loss / GPU toggle), or on resize.
        foreach (var layer in Layers.Values)
        {
            var needsNewSurface = layer.Surface is null;
            if (!needsNewSurface)
            {
                // Compare native handles: managed GRContext wrappers are not guaranteed unique
                var expectedContextHandle = useGpu ? grContext!.Handle : IntPtr.Zero;
                var layerContextHandle = layer.Surface!.Context?.Handle ?? IntPtr.Zero;
                if (layerContextHandle != expectedContextHandle)
                {
                    needsNewSurface = true;
                }
                else
                {
                    // Check if we need to resize
                    var currentInfo = layer.Surface!.Canvas.DeviceClipBounds;
                    needsNewSurface =
                        currentInfo.Width != CanvasSize.Width || currentInfo.Height != CanvasSize.Height;
                }
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
                            logger.LogWarning("GPU surface creation failed, falling back to CPU for layer");
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

        // Render all layer images in order. layer.Bitmaps is an atomically-swapped immutable list;
        // bitmaps swapped out mid-frame stay alive in retiredLayerBitmaps until the frame completes.
        foreach (var (layerName, layer) in Layers)
        {
            // Skip background image if not requested
            if (!renderBackgroundImage && layerName == "Background")
            {
                continue;
            }

            var layerCanvas = layer.Surface!.Canvas;
            foreach (var bitmap in layer.Bitmaps)
            {
                layerCanvas.DrawBitmap(bitmap, new SKPoint(0, 0));
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
            layer.Surface!.Canvas.Flush();
            surface.Canvas.DrawSurface(layer.Surface!, new SKPoint(0, 0));
        }

        // Draw grid overlay if enabled
        if (ShowGridOverlay)
        {
            RenderGridOverlay(surface.Canvas);
        }

        surface.Canvas.Flush();

        // The frame is fully drawn and flushed - bitmaps retired by UI-thread layer swaps can no
        // longer be referenced by this pass, so free them now, on the thread that owns rendering.
        while (retiredLayerBitmaps.TryDequeue(out var retired))
        {
            retired.Dispose();
        }
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

        using var paint = new SKPaint();
        paint.Shader = cachedCheckerboardShader;
        paint.IsAntialias = false;

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
        if (CanvasSize == Size.Empty)
            return;

        RenderGridOverlayCore(canvas, CanvasSize.Width, CanvasSize.Height, GridDivisions);
    }

    private static void RenderGridOverlayCore(SKCanvas canvas, int width, int height, int gridDivisions)
    {
        if (gridDivisions <= 1)
            return;

        using var paint = new SKPaint
        {
            Color = GridLineColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
        };

        // Draw vertical lines
        for (var i = 1; i < gridDivisions; i++)
        {
            var x = (float)(width * i) / gridDivisions;
            canvas.DrawLine(x, 0, x, height, paint);
        }

        // Draw horizontal lines
        for (var i = 1; i < gridDivisions; i++)
        {
            var y = (float)(height * i) / gridDivisions;
            canvas.DrawLine(0, y, width, y, paint);
        }
    }

    /// <summary>
    /// Renders paths with caching optimization. Completed paths are cached
    /// to avoid re-rendering them every frame.
    /// </summary>
    private void RenderPathsWithCaching(SKCanvas paintLayerCanvas)
    {
        // Consume a pending invalidation: the cache is owned by this (render) thread, so this is
        // where the stale image actually gets disposed and reset. Atomic read-and-reset so a
        // concurrent UI-thread invalidation is never lost between a check and a clear.
        if (Interlocked.Exchange(ref pathCacheDirty, 0) == 1)
        {
            cachedPathsImage?.Dispose();
            cachedPathsImage = null;
            cachedPathsCount = 0;
        }

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

        // Render in-progress strokes directly (the batched rendering is already optimized)
        foreach (var stroke in TemporaryPaths.Values)
        {
            RenderLiveStroke(paintLayerCanvas, stroke, paint);
        }
    }

    /// <summary>
    /// Updates the path cache with all current completed paths.
    /// Uses CPU-only surfaces to avoid GPU context threading issues.
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

        // Always use CPU surface for cache to avoid GPU context threading issues
        // The cache is created once per set of completed paths, so CPU performance is acceptable
        var cacheSurface = SKSurface.Create(imageInfo);

        if (cacheSurface == null)
        {
            logger.LogWarning("Failed to create cache surface");
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
                logger.LogDebug("Updated path cache with {Count} paths (CPU surface)", cachedPathsCount);
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
        // Handle shape path types (Rectangle, Ellipse, Bitmap)
        switch (penPath.PathType)
        {
            case PenPathType.Rectangle:
            case PenPathType.Ellipse:
                RenderShapePath(canvas, penPath, paint, overrideColor);
                return;

            case PenPathType.Bitmap:
                RenderBitmapPath(canvas, penPath, paint, overrideColor);
                return;

            case PenPathType.Freehand:
            default:
                // Continue with freehand rendering below
                RenderFreehandPath(canvas, penPath, paint, overrideColor);
                return;
        }
    }

    /// <summary>
    /// Renders shape paths (Rectangle and Ellipse) to the canvas.
    /// </summary>
    private static void RenderShapePath(
        SKCanvas canvas,
        PenPath penPath,
        SKPaint paint,
        SKColor? overrideColor
    )
    {
        // Apply color and blend mode
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

        if (penPath.IsStrokeOnly)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = penPath.StrokeWidth;
        }
        else
        {
            paint.Style = SKPaintStyle.Fill;
        }

        if (penPath.PathType == PenPathType.Rectangle)
        {
            canvas.DrawRect(penPath.Bounds, paint);
        }
        else // Ellipse
        {
            canvas.DrawOval(penPath.Bounds, paint);
        }
    }

    /// <summary>
    /// Renders bitmap paths to the canvas with optional color override.
    /// </summary>
    private static void RenderBitmapPath(
        SKCanvas canvas,
        PenPath penPath,
        SKPaint paint,
        SKColor? overrideColor
    )
    {
        if (penPath.BitmapData == null)
            return;

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
            canvas.DrawBitmap(penPath.BitmapData, penPath.Bounds.Left, penPath.Bounds.Top, colorPaint);
        }
        else
        {
            canvas.DrawBitmap(penPath.BitmapData, penPath.Bounds.Left, penPath.Bounds.Top);
        }
    }

    /// <summary>
    /// Renders an in-progress <see cref="LiveStroke"/> to a canvas using a stable point snapshot,
    /// safe to call from the render thread while the UI thread keeps appending points.
    /// </summary>
    public static void RenderLiveStroke(
        SKCanvas canvas,
        LiveStroke stroke,
        SKPaint paint,
        SKColor? overrideColor = null
    )
    {
        var template = stroke.Template;

        switch (template.PathType)
        {
            case PenPathType.Rectangle:
            case PenPathType.Ellipse:
                RenderShapePath(canvas, template, paint, overrideColor);
                return;

            case PenPathType.Bitmap:
                RenderBitmapPath(canvas, template, paint, overrideColor);
                return;

            case PenPathType.Freehand:
            default:
                RenderFreehandPathCore(canvas, template, stroke.GetPointsSnapshot(), paint, overrideColor);
                return;
        }
    }

    /// <summary>
    /// Renders freehand paths with pressure-sensitive strokes to the canvas.
    /// </summary>
    private static void RenderFreehandPath(
        SKCanvas canvas,
        PenPath penPath,
        SKPaint paint,
        SKColor? overrideColor = null
    ) => RenderFreehandPathCore(canvas, penPath, penPath.Points, paint, overrideColor);

    /// <summary>
    /// Shared freehand rendering over any stable point list: a finalized path's own
    /// (frozen) list, or a <see cref="LiveStroke"/> snapshot array.
    /// </summary>
    private static void RenderFreehandPathCore(
        SKCanvas canvas,
        PenPath penPath,
        IReadOnlyList<PenPoint> points,
        SKPaint paint,
        SKColor? overrideColor = null
    )
    {
        // Freehand path rendering. The point list is always a stable snapshot here: finalized
        // PenPath lists are frozen at finalize time, and LiveStroke hands out immutable arrays.
        var pointCount = points.Count;

        if (pointCount == 0)
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

        // Get effective radius (path-level, or backward-compat from the first point —
        // mirrors PenPath.GetEffectiveRadius but reads the caller-supplied point list)
        var effectiveRadius =
            penPath.Radius > 0 ? penPath.Radius
            : pointCount > 0 && points[0].Radius > 0 ? (float)points[0].Radius
            : 1f;

        // Apply feathering (soft brush edge) using blur mask filter
        if (penPath.Feathering > 0)
        {
            // Calculate blur sigma based on the effective radius and feathering amount
            var blurSigma = effectiveRadius * penPath.Feathering * 0.5f;
            if (blurSigma > 0.1f)
            {
                paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blurSigma);
            }
        }
        else
        {
            paint.MaskFilter = null;
        }

        // Count pen points and check pressure uniformity in a single pass (avoids LINQ allocations)
        var penPointCount = 0;
        var uniformPressure = true;
        var firstPressure = 0.0;
        var firstPenPointIndex = -1;

        for (var i = 0; i < pointCount; i++)
        {
            var p = points[i];
            if (!p.IsPen)
                continue;

            var pressure = p.Pressure ?? 1;

            if (penPointCount == 0)
            {
                firstPressure = pressure;
                firstPenPointIndex = i;
            }
            else if (uniformPressure && Math.Abs(pressure - firstPressure) >= 0.1)
            {
                uniformPressure = false;
            }

            penPointCount++;
        }

        if (penPointCount == 0)
        {
            // No pen points - draw a plain polyline for mouse-based paths
            paint.StrokeWidth = effectiveRadius * 2;
            using var skPath = BuildSKPath(points, pointCount);
            canvas.DrawPath(skPath, paint);
            return;
        }

        // For pressure-sensitive drawing, we need to handle variable thickness
        if (penPointCount == 1)
        {
            // Single point - draw a circle
            var point = points[firstPenPointIndex];
            var thickness = (point.Pressure ?? 1) * effectiveRadius * 2.5;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(point.X, point.Y, (float)(thickness / 2), paint);
            return;
        }

        if (uniformPressure)
        {
            // All points have similar pressure - batch into a single path. Width comes from the
            // FIRST point's pressure, which never changes as the stroke grows. A running average
            // here made the whole in-progress stroke re-render wider/narrower every frame as new
            // points shifted the mean ("breathing" while drawing).
            paint.StrokeWidth = (float)(firstPressure * effectiveRadius * 2.5);

            using var path = new SKPath();
            var started = false;

            // Use plain loop instead of LINQ to avoid iterator allocation in hot path
            for (var i = 0; i < pointCount; i++)
            {
                var p = points[i];
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

            for (var i = 0; i < pointCount; i++)
            {
                var point = points[i];
                if (!point.IsPen)
                    continue;

                var thickness = (float)((point.Pressure ?? 1) * effectiveRadius * 2.5);

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

    /// <summary>
    /// Builds a polyline <see cref="SKPath"/> from the first <paramref name="pointCount"/> entries
    /// of a stable point list. Caller owns the returned path.
    /// </summary>
    private static SKPath BuildSKPath(IReadOnlyList<PenPoint> points, int pointCount)
    {
        var skPath = new SKPath();

        if (pointCount <= 0)
        {
            return skPath;
        }

        skPath.MoveTo(points[0].X, points[0].Y);

        for (var i = 1; i < pointCount; i++)
        {
            skPath.LineTo(points[i].X, points[i].Y);
        }

        return skPath;
    }

    /// <summary>
    /// Disposes all cached resources to free memory. Called from the UI thread.
    /// Quiesces rendering first: sets <see cref="_disposed"/> (checked at render entry) and
    /// waits for the in-flight render pass to exit before freeing the native resources it
    /// may be drawing with.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // New render passes see this at entry and return without touching resources
        _disposed = true;

        // Wait (bounded) for an in-flight render pass to finish. Frames are short; if this
        // ever times out something is badly wrong, so log and proceed rather than hang.
        var waitStart = Environment.TickCount64;
        while (Volatile.Read(ref rendersInFlight) > 0)
        {
            if (Environment.TickCount64 - waitStart > 1000)
            {
                logger.LogWarning(
                    "Dispose: timed out waiting for in-flight render pass ({Count} still active), proceeding",
                    Volatile.Read(ref rendersInFlight)
                );
                break;
            }

            // Sleep rather than yield: yielding in a tight loop busy-spins a core when no other
            // thread is ready on it; frames are short so a 1ms granularity wait is plenty
            Thread.Sleep(1);
        }

        // Dispose cached path image
        cachedPathsImage?.Dispose();
        cachedPathsImage = null;

        // Dispose checkerboard shader
        cachedCheckerboardShader?.Dispose();
        cachedCheckerboardShader = null;

        // Dispose layer surfaces and bitmaps
        foreach (var layer in Layers.Values)
        {
            layer.Surface?.Dispose();
            layer.Surface = null;

            foreach (var bitmap in layer.Bitmaps)
            {
                bitmap.Dispose();
            }
            layer.Bitmaps = [];
        }

        // Drain bitmaps that were retired by layer swaps but never freed by a render pass
        while (retiredLayerBitmaps.TryDequeue(out var retired))
        {
            retired.Dispose();
        }

        // Dispose flood-fill bitmap data owned by paths (Paths, the undo redoStack, and any
        // in-progress TemporaryPaths). PenPath.BitmapData is an SKBitmap set by FloodFillAt and
        // is otherwise never disposed.
        foreach (var penPath in Paths)
        {
            penPath.BitmapData?.Dispose();
        }

        foreach (var penPath in redoStack)
        {
            penPath.BitmapData?.Dispose();
        }

        foreach (var stroke in TemporaryPaths.Values)
        {
            stroke.Template.BitmapData?.Dispose();
        }

        // Clear paths
        TemporaryPaths.Clear();

        GC.SuppressFinalize(this);
    }
}
