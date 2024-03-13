using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.Models;

public readonly record struct PenPoint
{
    public PenPoint(SKPoint point, double radius = 1, double? pressure = null)
    {
        Point = point;
        Radius = radius;
        Pressure = pressure;
    }

    public SKPoint Point { get; init; }

    public double Radius { get; init; }

    public double? Pressure { get; init; }
}
