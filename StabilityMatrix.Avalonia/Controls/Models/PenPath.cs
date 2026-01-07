using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkiaSharp;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Avalonia.Controls.Models;

/// <summary>
/// Type of path - determines how the path is rendered.
/// </summary>
public enum PenPathType
{
    /// <summary>
    /// Freehand brush strokes (default).
    /// </summary>
    Freehand,

    /// <summary>
    /// Filled rectangle shape.
    /// </summary>
    Rectangle,

    /// <summary>
    /// Filled ellipse/oval shape.
    /// </summary>
    Ellipse,

    /// <summary>
    /// Bitmap image (used for flood fill results).
    /// </summary>
    Bitmap,
}

public readonly record struct PenPath()
{
    [JsonConverter(typeof(SKColorJsonConverter))]
    public SKColor FillColor { get; init; }

    public bool IsErase { get; init; }

    /// <summary>
    /// Type of path (Freehand, Rectangle, or Ellipse).
    /// </summary>
    public PenPathType PathType { get; init; } = PenPathType.Freehand;

    /// <summary>
    /// Bounding rectangle for shape paths (Rectangle, Ellipse).
    /// For Freehand paths, this is ignored.
    /// </summary>
    [JsonConverter(typeof(SKRectJsonConverter))]
    public SKRect Bounds { get; init; }

    /// <summary>
    /// If true, draws shape outline only (stroke). If false, fills the shape.
    /// Only applies to Rectangle and Ellipse path types.
    /// </summary>
    public bool IsStrokeOnly { get; init; }

    /// <summary>
    /// Stroke width for stroke-only shapes. Only used when IsStrokeOnly is true.
    /// </summary>
    public float StrokeWidth { get; init; } = 5f;

    public List<PenPoint> Points { get; init; } = [];

    /// <summary>
    /// Bitmap data for flood fill paths. Stored as PNG bytes for serialization.
    /// </summary>
    [JsonIgnore]
    public SKBitmap? BitmapData { get; init; }

    public SKPath ToSKPath()
    {
        var skPath = new SKPath();

        if (Points.Count <= 0)
        {
            return skPath;
        }

        // First move to the first point
        skPath.MoveTo(Points[0].X, Points[0].Y);

        // Add the rest of the points
        for (var i = 1; i < Points.Count; i++)
        {
            skPath.LineTo(Points[i].X, Points[i].Y);
        }

        return skPath;
    }
}
