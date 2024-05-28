using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

[Transient]
[ManagedService]
public partial class PaintCanvasViewModel : LoadableViewModelBase
{
    public ConcurrentDictionary<long, PenPath> TemporaryPaths { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private ImmutableList<PenPath> paths = [];

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
    [property: JsonIgnore]
    private SKBitmap? backgroundImage;

    [JsonIgnore]
    public List<SKBitmap> LayerImages { get; } = [];

    /// <summary>
    /// Set by <see cref="PaintCanvas"/> to allow the view model to take a snapshot of the canvas.
    /// </summary>
    [JsonIgnore]
    public Func<SKImage>? GetCanvasSnapshot { get; set; }

    /// <summary>
    /// Set by <see cref="PaintCanvas"/> to allow the view model to
    /// refresh the canvas view after updating points or bitmap layers.
    /// </summary>
    [JsonIgnore]
    public Action? RefreshCanvas { get; set; }

    public void LoadCanvasFromBitmap(SKBitmap bitmap)
    {
        LayerImages.Clear();
        LayerImages.Add(bitmap);

        RefreshCanvas?.Invoke();
    }

    private bool CanExecuteUndo()
    {
        return Paths.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
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

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        base.LoadStateFromJsonObject(state);

        RefreshCanvas?.Invoke();
    }

    protected PaintCanvasModel SaveCanvas()
    {
        var model = new PaintCanvasModel
        {
            TemporaryPaths = TemporaryPaths.ToDictionary(x => x.Key, x => x.Value),
            Paths = Paths,
            PaintBrushColor = PaintBrushColor,
            PaintBrushSize = PaintBrushSize,
            PaintBrushAlpha = PaintBrushAlpha
        };

        return model;
    }

    protected void LoadCanvas(PaintCanvasModel model)
    {
        TemporaryPaths.Clear();
        foreach (var (key, value) in model.TemporaryPaths)
        {
            TemporaryPaths.TryAdd(key, value);
        }

        Paths = model.Paths;
        PaintBrushColor = model.PaintBrushColor;
        PaintBrushSize = model.PaintBrushSize;
        PaintBrushAlpha = model.PaintBrushAlpha;

        RefreshCanvas?.Invoke();
    }

    protected class PaintCanvasModel
    {
        public Dictionary<long, PenPath> TemporaryPaths { get; init; } = new();

        public ImmutableList<PenPath> Paths { get; init; } = ImmutableList<PenPath>.Empty;

        public Color? PaintBrushColor { get; init; }

        public double PaintBrushSize { get; init; }

        public double PaintBrushAlpha { get; init; }
    }
}
