using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.Models;

public readonly record struct PenPoint(SKPoint Point)
{
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
}
