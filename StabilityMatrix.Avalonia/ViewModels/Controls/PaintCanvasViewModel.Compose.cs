using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

public partial class PaintCanvasViewModel
{
    /// <summary>
    /// An immutable snapshot of everything needed to composite the canvas, captured on the
    /// UI thread. Off-screen exports compose exclusively from these inputs onto CPU surfaces
    /// they own, so they never touch the persistent layer surfaces that belong to the
    /// on-screen render pass (see <see cref="RenderToSurface"/>).
    /// </summary>
    private readonly record struct ComposeInputs(
        int Width,
        int Height,
        ImmutableList<SKBitmap> BackgroundBitmaps,
        ImmutableList<SKBitmap> ImagesBitmaps,
        ImmutableList<SKBitmap> BrushBitmaps,
        ImmutableList<SKBitmap> OverlayBitmaps,
        ImmutableList<PenPath> Paths,
        LiveStroke[] LiveStrokes,
        bool ShowGrid,
        int GridDivisions
    );

    /// <summary>
    /// Captures the current canvas state as immutable references. The bitmap lists are
    /// atomically-swapped <see cref="ImmutableList{T}"/> instances, finalized paths are an
    /// immutable list, and live strokes provide stable point snapshots — so the returned
    /// value can be composed from without any locks.
    /// </summary>
    private ComposeInputs CaptureComposeInputs(bool renderBackgroundImage)
    {
        return new ComposeInputs(
            CanvasSize.Width,
            CanvasSize.Height,
            renderBackgroundImage ? BackgroundLayer.Bitmaps : [],
            ImagesLayer.Bitmaps,
            BrushLayer.Bitmaps,
            OverlayLayer.Bitmaps,
            Paths,
            TemporaryPaths.Values.ToArray(),
            ShowGridOverlay,
            GridDivisions
        );
    }

    /// <summary>
    /// Pure compositor: renders a snapshot of the canvas onto the target canvas. Creates its
    /// own scratch CPU surface for the brush layer (so erase strokes clear brush content only,
    /// matching the on-screen per-layer compositing) and never reads or mutates shared
    /// surface state.
    /// </summary>
    private static void ComposeToCanvas(SKCanvas target, in ComposeInputs inputs)
    {
        target.Clear(SKColors.Transparent);

        // Background and Images layers contain only plain bitmap draws, so compositing them
        // through an intermediate surface is equivalent to drawing them directly.
        foreach (var bitmap in inputs.BackgroundBitmaps)
        {
            target.DrawBitmap(bitmap, 0, 0);
        }

        foreach (var bitmap in inputs.ImagesBitmaps)
        {
            target.DrawBitmap(bitmap, 0, 0);
        }

        // The brush layer needs isolation: erase strokes use SKBlendMode.Clear and must only
        // erase brush-layer content, not the layers beneath.
        using (var brushSurface = SKSurface.Create(new SKImageInfo(inputs.Width, inputs.Height)))
        {
            if (brushSurface is not null)
            {
                var brushCanvas = brushSurface.Canvas;
                brushCanvas.Clear(SKColors.Transparent);

                foreach (var bitmap in inputs.BrushBitmaps)
                {
                    brushCanvas.DrawBitmap(bitmap, 0, 0);
                }

                using var paint = new SKPaint();

                foreach (var penPath in inputs.Paths)
                {
                    RenderPenPath(brushCanvas, penPath, paint);
                }

                foreach (var stroke in inputs.LiveStrokes)
                {
                    RenderLiveStroke(brushCanvas, stroke, paint);
                }

                brushCanvas.Flush();
                target.DrawSurface(brushSurface, new SKPoint(0, 0));
            }
        }

        foreach (var bitmap in inputs.OverlayBitmaps)
        {
            target.DrawBitmap(bitmap, 0, 0);
        }

        if (inputs.ShowGrid)
        {
            RenderGridOverlayCore(target, inputs.Width, inputs.Height, inputs.GridDivisions);
        }

        target.Flush();
    }

    /// <summary>
    /// Composes a snapshot of the canvas into a new CPU-backed <see cref="SKImage"/>.
    /// Safe to call from the UI thread at any time; does not interact with the on-screen
    /// render pass or its surfaces.
    /// </summary>
    private SKImage? ComposeToNewImage(bool renderBackgroundImage)
    {
        if (CanvasSize == Size.Empty)
        {
            logger.LogWarning($"ComposeToNewImage: {nameof(CanvasSize)} is not set, returning null.");
            return null;
        }

        var inputs = CaptureComposeInputs(renderBackgroundImage);

        // SKSurface.Create can return null under low memory
        using var surface = SKSurface.Create(new SKImageInfo(inputs.Width, inputs.Height));
        if (surface is null)
        {
            logger.LogWarning("ComposeToNewImage: Failed to create surface, returning null.");
            return null;
        }

        ComposeToCanvas(surface.Canvas, inputs);

        return surface.Snapshot();
    }
}
