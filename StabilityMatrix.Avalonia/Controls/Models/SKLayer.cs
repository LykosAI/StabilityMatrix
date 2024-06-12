using System.Collections.Immutable;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls.Models;

public class SKLayer
{
    /// <summary>
    /// Surface from Canvas that contains the layer.
    /// </summary>
    public SKSurface? Surface { get; set; }

    /// <summary>
    /// Optional bitmaps that will be drawn on the layer, in order.
    /// (Last index will be drawn on top over previous ones)
    /// </summary>
    public ImmutableList<SKBitmap> Bitmaps { get; set; } = [];
}
