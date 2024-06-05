using System;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.Models;

public readonly record struct PenPoint(ulong X, ulong Y)
{
    public PenPoint(double x, double y)
        : this(Convert.ToUInt64(x), Convert.ToUInt64(y)) { }

    public PenPoint(SKPoint skPoint)
        : this(Convert.ToUInt64(skPoint.X), Convert.ToUInt64(skPoint.Y)) { }

    /// <summary>
    /// Radius of the point.
    /// </summary>
    public double Radius { get; init; } = 1;

    /// <summary>
    /// Optional pressure of the point. If null, the pressure is unknown.
    /// </summary>
    public double? Pressure { get; init; }

    /// <summary>
    /// True if the point was created by a pen, false if it was created by a mouse.
    /// </summary>
    public bool IsPen { get; init; }

    public SKPoint ToSKPoint() => new(X, Y);
}
