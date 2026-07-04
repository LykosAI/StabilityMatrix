using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;

namespace StabilityMatrix.UITests;

/// <summary>
/// Phase 0 characterization tests guarding the JSON contract for <see cref="PenPath"/> /
/// <see cref="PenPoint"/>. These are the persisted representation of paint-canvas strokes and
/// mask layers; every later threading-redesign phase must keep this contract byte-stable so old
/// projects keep loading. Two independent serialization paths are exercised:
/// <list type="bullet">
///   <item>Path A: the source-gen serializer used by
///   <c>PaintCanvasViewModel.SaveStateToJsonObject</c> (via <c>PaintCanvasModelSerializerContext</c>).</item>
///   <item>Path B: the reflection path used by <c>MaskLayer.SaveStateToJsonObject</c>
///   (<c>JsonSerializer.SerializeToNode(List&lt;PenPath&gt;)</c> then <c>Deserialize&lt;List&lt;PenPath&gt;&gt;</c>).</item>
/// </list>
///
/// Members intentionally NOT persisted (see [JsonIgnore] on the models):
/// <list type="bullet">
///   <item><see cref="PenPoint.Radius"/> — legacy per-point radius; new paths carry radius on <see cref="PenPath.Radius"/>.</item>
///   <item><see cref="PenPoint.IsPen"/> — not persisted directly, but inferred on decompress (Phase 4b):
///   a mouse point (null Pressure, IsPen false) is written with a -1 pressure sentinel and round-trips as a
///   mouse point; any written pressure in [0, 1] round-trips as a pen point.</item>
///   <item><see cref="PenPath.Points"/> — the property itself is [JsonIgnore]; points are serialized by the
///   custom converter as the compressed <c>points</c> string, so they DO round-trip (just not as a JSON array).</item>
/// </list>
///
/// Contract quirks that these tests pin (current behavior = correct-by-definition):
/// <list type="bullet">
///   <item><see cref="PenPoint.X"/>/<see cref="PenPoint.Y"/> are <c>ulong</c>; the point is compressed as a
///   <c>float</c> then read back and clamped to non-negative. So X/Y round-trip exactly for small integers.</item>
///   <item><see cref="PenPoint.Pressure"/> is stored as a float; on decompress a value is only kept when it lies
///   in [0, 1], otherwise the point becomes a mouse point (null Pressure, IsPen false). A pen point's null
///   pressure is written as 1.0 and comes back as 1.0; a mouse point's null is written as -1 and comes back
///   as null.</item>
/// </list>
/// </summary>
public class PaintCanvasSerializationTests
{
    private static List<PenPoint> BuildVariedPoints()
    {
        // ~10 points, varied Pressure (including null and an out-of-range value that must clamp to null),
        // varied Radius (a NOT-persisted member), IsPen true/false (also NOT persisted).
        return
        [
            new PenPoint(0, 0)
            {
                Pressure = 0.0,
                Radius = 3,
                IsPen = true,
            },
            new PenPoint(10, 5)
            {
                Pressure = 0.25,
                Radius = 4,
                IsPen = false,
            },
            new PenPoint(20, 12)
            {
                Pressure = 0.5,
                Radius = 5,
                IsPen = true,
            },
            new PenPoint(30, 20)
            {
                Pressure = null,
                Radius = 6,
                IsPen = true,
            },
            new PenPoint(45, 33)
            {
                Pressure = 0.75,
                Radius = 2,
                IsPen = false,
            },
            new PenPoint(60, 40)
            {
                Pressure = 1.0,
                Radius = 7,
                IsPen = true,
            },
            new PenPoint(75, 55)
            {
                Pressure = null,
                Radius = 1,
                IsPen = false,
            },
            new PenPoint(90, 70)
            {
                Pressure = 0.33,
                Radius = 8,
                IsPen = true,
            },
            new PenPoint(120, 90)
            {
                Pressure = 0.9,
                Radius = 9,
                IsPen = true,
            },
            new PenPoint(150, 110)
            {
                Pressure = 0.15,
                Radius = 10,
                IsPen = false,
            },
        ];
    }

    private static PenPath BuildFreehandPath()
    {
        return new PenPath
        {
            Points = BuildVariedPoints(),
            FillColor = new SKColor(12, 34, 56, 200),
            IsErase = true,
            Feathering = 0.4f,
            StrokeWidth = 7.5f,
            Radius = 6.25f,
            IsStrokeOnly = true,
            PathType = PenPathType.Freehand,
        };
    }

    private static PenPath BuildRectanglePath()
    {
        return new PenPath
        {
            FillColor = new SKColor(255, 0, 0, 255),
            PathType = PenPathType.Rectangle,
            Bounds = new SKRect(4, 8, 40, 60),
            IsStrokeOnly = false,
            StrokeWidth = 5f,
        };
    }

    private static PenPath BuildEllipsePath()
    {
        return new PenPath
        {
            FillColor = new SKColor(0, 128, 255, 128),
            PathType = PenPathType.Ellipse,
            Bounds = new SKRect(10, 10, 50, 30),
            IsStrokeOnly = true,
            StrokeWidth = 3f,
        };
    }

    private static PenPath BuildBitmapPath()
    {
        // A small deterministic bitmap so the base64 PNG round-trip is exercised.
        var bitmap = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                bitmap.SetPixel(x, y, (x + y) % 2 == 0 ? SKColors.Lime : SKColors.Transparent);
            }
        }

        return new PenPath
        {
            FillColor = new SKColor(0, 255, 0, 255),
            PathType = PenPathType.Bitmap,
            BitmapData = bitmap,
            Bounds = new SKRect(0, 0, 4, 4),
        };
    }

    private static List<PenPath> BuildAllVariants() =>
        [BuildFreehandPath(), BuildRectanglePath(), BuildEllipsePath(), BuildBitmapPath()];

    /// <summary>
    /// Computes the expected post-round-trip (Pressure, IsPen) for a point, mirroring the
    /// compress/decompress contract: written pressure = Pressure ?? (IsPen ? 1.0 : -1.0);
    /// on read, values in [0, 1] are pen points, anything else is a mouse point with null pressure.
    /// </summary>
    private static (double? Pressure, bool IsPen) ExpectedPointRoundTrip(PenPoint point)
    {
        var written = point.Pressure ?? (point.IsPen ? 1.0 : -1.0);
        var isPen = written is >= 0 and <= 1;
        return (isPen ? written : null, isPen);
    }

    /// <summary>
    /// Asserts full value equality of the persisted members after a round-trip.
    /// Explicitly does NOT compare PenPoint.Radius (not persisted).
    /// </summary>
    private static void AssertPathEqual(PenPath expected, PenPath actual)
    {
        Assert.Equal(expected.FillColor, actual.FillColor);
        Assert.Equal(expected.IsErase, actual.IsErase);
        Assert.Equal(expected.PathType, actual.PathType);
        Assert.Equal(expected.Bounds, actual.Bounds);
        Assert.Equal(expected.IsStrokeOnly, actual.IsStrokeOnly);
        Assert.Equal(expected.StrokeWidth, actual.StrokeWidth);
        Assert.Equal(expected.Radius, actual.Radius);
        Assert.Equal(expected.Feathering, actual.Feathering);

        // Points: compare via the compressed/persisted representation.
        // X/Y are ulong and round-trip exactly for our small integer coordinates.
        Assert.Equal(expected.Points.Count, actual.Points.Count);
        for (var i = 0; i < expected.Points.Count; i++)
        {
            var e = expected.Points[i];
            var a = actual.Points[i];
            Assert.Equal(e.X, a.X);
            Assert.Equal(e.Y, a.Y);

            var (expectedPressure, expectedIsPen) = ExpectedPointRoundTrip(e);
            Assert.Equal(expectedIsPen, a.IsPen);

            if (expectedPressure is { } pressure)
            {
                // Pressure is compressed as a float, so a double like 0.33 comes back as the nearest
                // float (0.3300000131...). Compare through a float cast to characterize that precision loss.
                Assert.NotNull(a.Pressure);
                Assert.Equal((float)pressure, (float)a.Pressure!.Value);
            }
            else
            {
                Assert.Null(a.Pressure);
            }
        }

        // Bitmap data: compare dimensions and a sampling of pixels if present.
        if (expected.BitmapData is { } expectedBitmap)
        {
            Assert.NotNull(actual.BitmapData);
            Assert.Equal(expectedBitmap.Width, actual.BitmapData!.Width);
            Assert.Equal(expectedBitmap.Height, actual.BitmapData.Height);
            for (var y = 0; y < expectedBitmap.Height; y++)
            {
                for (var x = 0; x < expectedBitmap.Width; x++)
                {
                    Assert.Equal(expectedBitmap.GetPixel(x, y), actual.BitmapData.GetPixel(x, y));
                }
            }
        }
        else
        {
            Assert.Null(actual.BitmapData);
        }
    }

    // ---- Path B: reflection path used by MaskLayer.cs (SerializeToNode(List<PenPath>) -> Deserialize) ----

    [AvaloniaFact]
    public void MaskLayerReflectionPath_RoundTripsAllVariants()
    {
        var original = BuildAllVariants();

        // Mirror of MaskLayer.SaveStateToJsonObject line ~417 / LoadStateFromJsonObject line ~381.
        var node = JsonSerializer.SerializeToNode(original);
        Assert.NotNull(node);
        Assert.IsType<JsonArray>(node);

        var roundTripped = node.Deserialize<List<PenPath>>();
        Assert.NotNull(roundTripped);
        Assert.Equal(original.Count, roundTripped!.Count);

        for (var i = 0; i < original.Count; i++)
        {
            AssertPathEqual(original[i], roundTripped[i]);
        }
    }

    [AvaloniaFact]
    public void MaskLayerReflectionPath_DoesNotPersistIgnoredPenPointMembers()
    {
        // PenPoint.Radius and PenPoint.IsPen are [JsonIgnore] and must not survive as-authored.
        var original = new List<PenPath> { BuildFreehandPath() };
        var node = JsonSerializer.SerializeToNode(original);
        var roundTripped = node.Deserialize<List<PenPath>>()!;

        var points = roundTripped[0].Points;
        var originalPoints = original[0].Points;

        // Radius is not persisted; the decompress path always reconstructs with the default (1).
        Assert.All(points, p => Assert.Equal(1d, p.Radius));

        // IsPen is inferred from the written pressure (Phase 4b): mouse points (null pressure,
        // IsPen false) round-trip as mouse points via the -1 sentinel; everything else is a pen point.
        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(ExpectedPointRoundTrip(originalPoints[i]).IsPen, points[i].IsPen);
        }
    }

    // ---- Path A: source-gen serializer used by PaintCanvasViewModel.Serializer.cs ----

    [AvaloniaFact]
    public void PaintCanvasViewModelState_RoundTripsPaths()
    {
        var original = BuildAllVariants();

        var save = TestHelpers.CreatePaintCanvasViewModel();
        save.Paths = original.ToImmutableList();

        // SaveStateToJsonObject serializes through PaintCanvasModelSerializerContext (source-gen).
        var state = save.SaveStateToJsonObject();

        var load = TestHelpers.CreatePaintCanvasViewModel();
        load.LoadStateFromJsonObject(state);

        Assert.Equal(original.Count, load.Paths.Count);
        for (var i = 0; i < original.Count; i++)
        {
            AssertPathEqual(original[i], load.Paths[i]);
        }
    }

    [AvaloniaFact]
    public void PaintCanvasViewModelState_PreservesScalarState()
    {
        var save = TestHelpers.CreatePaintCanvasViewModel();
        save.CanvasSize = new System.Drawing.Size(128, 96);
        save.PaintBrushSize = 21;
        save.PaintBrushAlpha = 0.5;
        save.SelectedTool = StabilityMatrix.Avalonia.Models.PaintCanvasTool.Eraser;

        var state = save.SaveStateToJsonObject();

        var load = TestHelpers.CreatePaintCanvasViewModel();
        load.LoadStateFromJsonObject(state);

        Assert.Equal(new System.Drawing.Size(128, 96), load.CanvasSize);
        Assert.Equal(21, load.PaintBrushSize);
        Assert.Equal(0.5, load.PaintBrushAlpha);
        Assert.Equal(StabilityMatrix.Avalonia.Models.PaintCanvasTool.Eraser, load.SelectedTool);
    }

    // ---- Byte-stability of the compressed points string ----

    [AvaloniaFact]
    public void CompressedPoints_AreByteStableAcrossSerializations()
    {
        var points = BuildVariedPoints();

        // The compressed string is what actually lands in the persisted JSON (the "points" member).
        var first = PenPath.CompressPointsPublic(points);
        var second = PenPath.CompressPointsPublic(points);

        Assert.NotNull(first);
        Assert.Equal(first, second);
    }

    [AvaloniaFact]
    public void SerializedPath_PointsMemberIsByteStable()
    {
        var path = BuildFreehandPath();

        var firstNode = (JsonObject)JsonSerializer.SerializeToNode(path)!;
        var secondNode = (JsonObject)JsonSerializer.SerializeToNode(path)!;

        var firstPoints = firstNode["points"]!.GetValue<string>();
        var secondPoints = secondNode["points"]!.GetValue<string>();

        Assert.Equal(firstPoints, secondPoints);

        // And the whole serialized object is stable too (guards the full converter output, not just points).
        Assert.Equal(firstNode.ToJsonString(), secondNode.ToJsonString());
    }

    // ---- Stroke finalization: LiveStroke -> PenPath ----

    /// <summary>
    /// <see cref="LiveStroke.ToPenPath"/> is used at stroke-finalize time. The finalized path must
    /// carry an equal, fully independent copy of the live points so later appends to the live
    /// stroke cannot leak into the path that was moved into the immutable <c>Paths</c> collection.
    /// </summary>
    [AvaloniaFact]
    public void LiveStrokeToPenPath_ProducesIndependentPointsList()
    {
        var template = BuildFreehandPath() with { Points = [] };
        var stroke = new LiveStroke { Template = template };
        stroke.AddPoints(BuildVariedPoints().ToArray());

        var finalized = stroke.ToPenPath();

        Assert.Equal(stroke.GetPointsSnapshot().Length, finalized.Points.Count);
        Assert.Equal(BuildVariedPoints(), finalized.Points);

        // Appending to the live stroke (as the pointer handler does) must not affect the
        // finalized copy.
        stroke.AddPoints([new PenPoint(999, 999) { Pressure = 1.0, IsPen = true }]);

        Assert.NotEqual(stroke.GetPointsSnapshot().Length, finalized.Points.Count);
    }
}
