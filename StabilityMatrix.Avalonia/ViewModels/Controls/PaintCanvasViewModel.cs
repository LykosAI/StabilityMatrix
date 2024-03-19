using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

public partial class PaintCanvasViewModel : ObservableObject
{
    [ObservableProperty]
    private Color? paintBrushColor;

    public SKColor PaintBrushSKColor => (PaintBrushColor ?? Colors.Transparent).ToSKColor();

    [ObservableProperty]
    private double paintBrushSize = 12;

    [ObservableProperty]
    private double paintBrushAlpha = 1;

    [ObservableProperty]
    private double currentPenPressure;

    [ObservableProperty]
    private double currentZoom;

    [ObservableProperty]
    private bool isPenDown;

    [ObservableProperty]
    private SKBitmap? backgroundImage;

    public Action<Stream>? SaveCanvasAsImage { get; set; }
}
