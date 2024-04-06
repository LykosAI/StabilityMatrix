using System.Collections.Generic;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.Models;

public readonly record struct PenPath(SKPath Path)
{
    public SKColor FillColor { get; init; }

    public bool IsErase { get; init; }

    public List<PenPoint> Points { get; init; } = [];
}
