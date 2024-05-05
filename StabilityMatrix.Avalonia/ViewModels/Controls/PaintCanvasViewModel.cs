using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    private bool isPaintBrushSelected;

    [ObservableProperty]
    private bool isEraserSelected;

    [ObservableProperty]
    private SKBitmap? backgroundImage;

    public List<SKBitmap> LayerImages { get; } = [];

    public Action<Stream>? LoadCanvasFromImage { get; set; }

    public Action<Stream>? SaveCanvasToImage { get; set; }

    public Action? RefreshCanvas { get; set; }

    public async Task SaveCanvasToJson(Stream stream)
    {
        var model = new PaintCanvasModel
        {
            TemporaryPaths = TemporaryPaths.ToDictionary(x => x.Key, x => x.Value),
            Paths = Paths,
            PaintBrushColor = PaintBrushColor,
            PaintBrushSize = PaintBrushSize,
            PaintBrushAlpha = PaintBrushAlpha
        };

        await JsonSerializer.SerializeAsync(stream, model);
    }

    public async Task LoadCanvasFromJson(Stream stream)
    {
        var model = await JsonSerializer.DeserializeAsync<PaintCanvasModel>(stream);

        TemporaryPaths.Clear();
        foreach (var (key, value) in model!.TemporaryPaths)
        {
            TemporaryPaths.TryAdd(key, value);
        }

        Paths = model.Paths;
        PaintBrushColor = model.PaintBrushColor;
        PaintBrushSize = model.PaintBrushSize;
        PaintBrushAlpha = model.PaintBrushAlpha;

        RefreshCanvas?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        // Remove last path
        var currentPaths = Paths;

        if (currentPaths.IsEmpty)
        {
            return;
        }

        Paths = currentPaths.RemoveAt(currentPaths.Count - 1);

        RefreshCanvas?.Invoke();
    }

    public class PaintCanvasModel
    {
        public Dictionary<long, PenPath> TemporaryPaths { get; set; } = new();

        public ImmutableList<PenPath> Paths { get; set; } = ImmutableList<PenPath>.Empty;

        public Color? PaintBrushColor { get; set; }

        public double PaintBrushSize { get; set; }

        public double PaintBrushAlpha { get; set; }
    }
}
