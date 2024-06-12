using System.Collections.Generic;
using System.Text.Json.Serialization;
using SkiaSharp;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Avalonia.Controls.Models;

public readonly record struct PenPath()
{
    [JsonConverter(typeof(SKColorJsonConverter))]
    public SKColor FillColor { get; init; }

    public bool IsErase { get; init; }

    public List<PenPoint> Points { get; init; } = [];

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
