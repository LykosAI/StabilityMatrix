using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.ViewModels.Controls;

namespace StabilityMatrix.UITests;

/// <summary>
/// Shared construction helpers for the paint-canvas Phase 0 characterization tests.
///
/// VM construction note (relevant to later phases): <see cref="PaintCanvasViewModel"/>'s only ctor
/// dependency is <c>ILogger&lt;PaintCanvasViewModel&gt;</c>, so it can be new'd directly with a
/// <see cref="NullLogger{T}"/> — no DI container / DialogFactory needed (DesignData.cs:1541 uses
/// <c>DialogFactory.Get&lt;PaintCanvasViewModel&gt;()</c> only because that's the app's ambient pattern).
/// GPU acceleration is disabled here so rendering is deterministic CPU-only Skia; the headless test
/// app is built with <c>UseHeadlessDrawing = false</c> + real Skia, so <c>RenderToImage()</c> works.
/// </summary>
public static class TestHelpers
{
    public static PaintCanvasViewModel CreatePaintCanvasViewModel()
    {
        return new PaintCanvasViewModel(NullLogger<PaintCanvasViewModel>.Instance)
        {
            // Deterministic CPU rendering; also avoids leaning on a GPU context that
            // the headless runner may not provide.
            UseGpuAcceleration = false,
            // The checkerboard is only painted when renderBackgroundFill is requested, but keep it
            // off so nothing bleeds into export characterization.
            ShowCheckerboardBackground = false,
        };
    }

    /// <summary>
    /// A freehand pen stroke with varied pressure, running left-to-right across the upper band.
    /// </summary>
    public static PenPath BuildPenStroke(SKColor color)
    {
        var points = new List<PenPoint>();
        for (var i = 0; i < 12; i++)
        {
            var x = (ulong)(6 + i * 4);
            var y = (ulong)(16 + (i % 3));
            var pressure = 0.3 + (i % 5) * 0.15; // varies 0.3 .. 0.9
            points.Add(new PenPoint(x, y) { Pressure = pressure, IsPen = true });
        }

        return new PenPath
        {
            Points = points,
            FillColor = color,
            Radius = 3f,
            PathType = PenPathType.Freehand,
        };
    }

    /// <summary>
    /// A mouse stroke (IsPen = false, no pressure) running along the lower band.
    /// </summary>
    public static PenPath BuildMouseStroke(SKColor color)
    {
        var points = new List<PenPoint>();
        for (var i = 0; i < 12; i++)
        {
            var x = (ulong)(6 + i * 4);
            var y = (ulong)46;
            points.Add(new PenPoint(x, y) { IsPen = false });
        }

        return new PenPath
        {
            Points = points,
            FillColor = color,
            Radius = 3f,
            PathType = PenPathType.Freehand,
        };
    }

    public static PenPath BuildRectangle(SKColor color, SKRect bounds)
    {
        return new PenPath
        {
            FillColor = color,
            PathType = PenPathType.Rectangle,
            Bounds = bounds,
            IsStrokeOnly = false,
        };
    }

    /// <summary>
    /// An erase stroke crossing the other content.
    /// </summary>
    public static PenPath BuildEraseStroke()
    {
        var points = new List<PenPoint>();
        for (var i = 0; i < 10; i++)
        {
            var x = (ulong)(20 + i * 2);
            var y = (ulong)(10 + i * 4);
            points.Add(new PenPoint(x, y) { IsPen = true, Pressure = 1.0 });
        }

        return new PenPath
        {
            Points = points,
            FillColor = SKColors.Transparent,
            IsErase = true,
            Radius = 4f,
            PathType = PenPathType.Freehand,
        };
    }
}
