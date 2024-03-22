using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

public partial class PaintCanvasViewModel : ObservableObject
{
    public ConcurrentDictionary<long, PenPath> TemporaryPaths { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndo))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private ImmutableList<PenPath> paths = [];

    public bool CanUndo => Paths.Count > 0;

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

    public Action<Stream>? LoadCanvasFromImage { get; set; }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public async Task UndoAsync()
    {
        // Remove last path
        if (Paths.Count > 0)
        {
            Paths = Paths.RemoveAt(Paths.Count - 1);
        }

        // await Dispatcher.UIThread.InvokeAsync(() => MainCanvas?.InvalidateVisual());
    }
}
