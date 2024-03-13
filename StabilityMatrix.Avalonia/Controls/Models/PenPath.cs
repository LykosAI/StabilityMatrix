using System.Collections.Generic;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.Models;

public readonly record struct PenPath
{
    public PenPath(SKPath path)
    {
        Path = path;
    }

    public SKPath Path { get; init; }

    public SKColor FillColor { get; init; }

    public List<PenPoint> Points { get; init; } = [];
}
