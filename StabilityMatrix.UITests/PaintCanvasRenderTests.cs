using System;
using System.Collections.Immutable;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.ViewModels.Controls;
using Xunit.Abstractions;

namespace StabilityMatrix.UITests;

/// <summary>
/// Phase 0 golden characterization of paint-canvas export rendering. Later refactor phases
/// (the threading redesign) must not silently change the exported image. Assertions are robust to
/// anti-aliasing: we do NOT hash whole images. Instead we pin the non-transparent pixel count within
/// a tolerance band, the painted bounding box within a few px, and exact colors at a handful of
/// interior sample points chosen after observing the current (correct-by-definition) output.
/// </summary>
public class PaintCanvasRenderTests
{
    private readonly ITestOutputHelper output;

    public PaintCanvasRenderTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private const int CanvasWidth = 64;
    private const int CanvasHeight = 64;

    private static readonly SKColor PenColor = new(255, 0, 0, 255); // red
    private static readonly SKColor MouseColor = new(0, 0, 255, 255); // blue
    private static readonly SKColor RectColor = new(0, 200, 0, 255); // green

    private static PaintCanvasViewModel BuildScene()
    {
        var vm = TestHelpers.CreatePaintCanvasViewModel();
        vm.CanvasSize = new System.Drawing.Size(CanvasWidth, CanvasHeight);

        // Deterministic scene: pen stroke (upper band), mouse stroke (lower band),
        // rectangle (mid), erase stroke crossing them.
        vm.Paths = ImmutableList.Create(
            TestHelpers.BuildPenStroke(PenColor),
            TestHelpers.BuildMouseStroke(MouseColor),
            TestHelpers.BuildRectangle(RectColor, new SKRect(30, 24, 58, 40)),
            TestHelpers.BuildEraseStroke()
        );

        return vm;
    }

    private static SKBitmap RenderToBitmap(SKImage image)
    {
        var bitmap = new SKBitmap(
            new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        Assert.True(image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap;
    }

    private static int CountNonTransparent(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 8)
                    count++;
            }
        }

        return count;
    }

    private static SKRectI PaintedBounds(SKBitmap bitmap)
    {
        int minX = bitmap.Width,
            minY = bitmap.Height,
            maxX = -1,
            maxY = -1;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha <= 8)
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return new SKRectI(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Diagnostic dump used once to derive the characterization constants below. Kept (skipped) so a
    /// future maintainer can re-derive expected values after an intentional rendering change.
    /// </summary>
    [AvaloniaFact(Skip = "Diagnostic only; run manually to re-derive characterization constants")]
    public void DumpCharacterization()
    {
        var vm = BuildScene();

        using var image = vm.RenderToImage()!;
        using var bitmap = RenderToBitmap(image);
        var bounds = PaintedBounds(bitmap);

        output.WriteLine($"RenderToImage non-transparent count = {CountNonTransparent(bitmap)}");
        output.WriteLine($"RenderToImage painted bounds = {bounds}");

        // Sample a grid of interior coordinates so we can pick stable ones.
        for (var y = 8; y < CanvasHeight; y += 8)
        {
            for (var x = 8; x < CanvasWidth; x += 8)
            {
                output.WriteLine($"  px ({x},{y}) = {bitmap.GetPixel(x, y)}");
            }
        }

        using var whiteImage = vm.RenderToWhiteChannelImage()!;
        using var whiteBitmap = RenderToBitmap(whiteImage);
        output.WriteLine(
            $"RenderToWhiteChannelImage non-transparent count = {CountNonTransparent(whiteBitmap)}"
        );
        output.WriteLine($"White painted bounds = {PaintedBounds(whiteBitmap)}");
        for (var y = 8; y < CanvasHeight; y += 8)
        {
            for (var x = 8; x < CanvasWidth; x += 8)
            {
                var p = whiteBitmap.GetPixel(x, y);
                if (p.Alpha > 8)
                    output.WriteLine($"  white px ({x},{y}) = {p}");
            }
        }
    }

    // ==== Characterization constants (derived from the current, correct-by-definition output) ====
    // Filled in after running DumpCharacterization once. See method above to regenerate.

    private const int ExpectedColorNonTransparent = 696;
    private const int ColorCountTolerance = 40;

    [AvaloniaFact]
    public void RenderToImage_NonTransparentPixelCount_WithinBand()
    {
        var vm = BuildScene();
        using var image = vm.RenderToImage()!;
        Assert.NotNull(image);
        using var bitmap = RenderToBitmap(image);

        var count = CountNonTransparent(bitmap);
        Assert.InRange(
            count,
            ExpectedColorNonTransparent - ColorCountTolerance,
            ExpectedColorNonTransparent + ColorCountTolerance
        );
    }

    [AvaloniaFact]
    public void RenderToImage_PaintedBounds_WithinTolerance()
    {
        var vm = BuildScene();
        using var image = vm.RenderToImage()!;
        using var bitmap = RenderToBitmap(image);
        var bounds = PaintedBounds(bitmap);

        AssertClose(bounds.Left, ExpectedBoundsLeft);
        AssertClose(bounds.Top, ExpectedBoundsTop);
        AssertClose(bounds.Right, ExpectedBoundsRight);
        AssertClose(bounds.Bottom, ExpectedBoundsBottom);
    }

    private const int ExpectedBoundsLeft = 3;
    private const int ExpectedBoundsTop = 12;
    private const int ExpectedBoundsRight = 57;
    private const int ExpectedBoundsBottom = 48;

    private static void AssertClose(int actual, int expected, int tolerance = 2)
    {
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    [AvaloniaFact]
    public void RenderToImage_SampledColors_MatchGolden()
    {
        var vm = BuildScene();
        using var image = vm.RenderToImage()!;
        using var bitmap = RenderToBitmap(image);

        foreach (var (x, y, expected) in ColorSamples)
        {
            var actual = bitmap.GetPixel(x, y);
            Assert.True(
                ColorsClose(actual, expected),
                $"pixel ({x},{y}) expected ~{expected} but was {actual}"
            );
        }
    }

    // Sample points chosen from the diagnostic dump: stroke centers, erased region, empty region.
    private static readonly (int X, int Y, SKColor Expected)[] ColorSamples =
    [
        (16, 16, new SKColor(255, 0, 0, 255)), // pen stroke (red)
        (40, 16, new SKColor(255, 0, 0, 255)), // pen stroke (red)
        (48, 32, new SKColor(0, 200, 0, 255)), // rectangle interior (green)
        (48, 24, new SKColor(0, 200, 0, 255)), // rectangle interior (green)
        (16, 48, new SKColor(0, 0, 255, 255)), // mouse stroke (blue)
        (40, 48, SKColors.Transparent), // erase stroke cleared this part of the mouse stroke
        (8, 8, SKColors.Transparent), // empty region
    ];

    private static bool ColorsClose(SKColor a, SKColor b, int tolerance = 20)
    {
        // When both are (near-)transparent, RGB is meaningless — Skia zeroes it to #00000000
        // while SKColors.Transparent is #00FFFFFF. Compare by alpha only in that case.
        if (a.Alpha <= tolerance && b.Alpha <= tolerance)
            return true;

        return Math.Abs(a.Red - b.Red) <= tolerance
            && Math.Abs(a.Green - b.Green) <= tolerance
            && Math.Abs(a.Blue - b.Blue) <= tolerance
            && Math.Abs(a.Alpha - b.Alpha) <= tolerance;
    }

    [AvaloniaFact]
    public void RenderToWhiteChannelImage_PaintsWhereColorImageDoes()
    {
        var vm = BuildScene();
        using var colorImage = vm.RenderToImage()!;
        using var colorBitmap = RenderToBitmap(colorImage);

        using var whiteImage = vm.RenderToWhiteChannelImage()!;
        Assert.NotNull(whiteImage);
        using var whiteBitmap = RenderToBitmap(whiteImage);

        // White-channel keeps original alpha but forces RGB to white; the painted footprint should
        // closely match the color render's footprint.
        var colorCount = CountNonTransparent(colorBitmap);
        var whiteCount = CountNonTransparent(whiteBitmap);
        Assert.InRange(whiteCount, colorCount - ColorCountTolerance, colorCount + ColorCountTolerance);

        // Every opaque white-channel pixel must be white (RGB).
        for (var y = 0; y < whiteBitmap.Height; y++)
        {
            for (var x = 0; x < whiteBitmap.Width; x++)
            {
                var p = whiteBitmap.GetPixel(x, y);
                if (p.Alpha <= 200)
                    continue;
                Assert.True(
                    p is { Red: >= 235, Green: >= 235, Blue: >= 235 },
                    $"white-channel pixel ({x},{y}) not white: {p}"
                );
            }
        }
    }

    [AvaloniaFact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var vm = BuildScene();
        // Force layer surfaces / caches to be allocated so Dispose has something to free.
        using (var image = vm.RenderToImage())
        {
            Assert.NotNull(image);
        }

        vm.Dispose();
        var exception = Record.Exception(() => vm.Dispose());
        Assert.Null(exception);
    }

    /// <summary>
    /// A flood-fill path carries an owned <see cref="SKBitmap"/> in <see cref="PenPath.BitmapData"/>
    /// (set by <c>FloodFillAt</c>) that is never otherwise disposed. Phase 1 of the threading redesign
    /// makes <see cref="PaintCanvasViewModel.Dispose"/> free it. SKBitmap's underlying native handle
    /// (<c>Handle</c>, public on SKObject) is reset to <see cref="IntPtr.Zero"/> once the wrapped native
    /// resource is released, so we use that to verify the bitmap was actually disposed rather than
    /// merely asserting Dispose doesn't throw. (SKObject also exposes <c>IsDisposed</c>, but it's not
    /// publicly accessible on the concrete SKBitmap type in this SkiaSharp 3.0 preview.)
    /// </summary>
    [AvaloniaFact]
    public void Dispose_DisposesFloodFillBitmapData()
    {
        var vm = TestHelpers.CreatePaintCanvasViewModel();
        vm.CanvasSize = new System.Drawing.Size(CanvasWidth, CanvasHeight);

        var bitmapData = new SKBitmap(CanvasWidth, CanvasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmapPath = new PenPath
        {
            PathType = PenPathType.Bitmap,
            FillColor = SKColors.Magenta,
            BitmapData = bitmapData,
            Bounds = new SKRect(0, 0, CanvasWidth, CanvasHeight),
        };

        vm.Paths = ImmutableList.Create(bitmapPath);

        Assert.NotEqual(IntPtr.Zero, bitmapData.Handle);

        vm.Dispose();

        Assert.Equal(IntPtr.Zero, bitmapData.Handle);
    }
}
