using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.Models;
using Color = Avalonia.Media.Color;

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

public partial class PaintCanvasViewModel
{
    public override JsonObject SaveStateToJsonObject()
    {
        var model = SaveState();

        return JsonSerializer
                .SerializeToNode(model, PaintCanvasModelSerializerContext.Default.Options)
                ?.AsObject() ?? throw new InvalidOperationException();
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = state.Deserialize<PaintCanvasModel>(PaintCanvasModelSerializerContext.Default.Options);

        if (model is null)
            return;

        LoadState(model);

        RefreshCanvas?.Invoke();
    }

    protected PaintCanvasModel SaveState()
    {
        var model = new PaintCanvasModel
        {
            TemporaryPaths = TemporaryPaths.ToDictionary(x => x.Key, x => x.Value),
            Paths = Paths,
            PaintBrushColor = PaintBrushColor,
            PaintBrushSize = PaintBrushSize,
            PaintBrushAlpha = PaintBrushAlpha,
            CurrentPenPressure = CurrentPenPressure,
            CurrentZoom = CurrentZoom,
            IsPenDown = IsPenDown,
            SelectedTool = SelectedTool,
            BackgroundImageSize = BackgroundImageSize
        };

        return model;
    }

    protected void LoadState(PaintCanvasModel model)
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
        CurrentPenPressure = model.CurrentPenPressure;
        CurrentZoom = model.CurrentZoom;
        IsPenDown = model.IsPenDown;
        SelectedTool = model.SelectedTool;
        BackgroundImageSize = model.BackgroundImageSize;

        RefreshCanvas?.Invoke();
    }

    public class PaintCanvasModel
    {
        public Dictionary<long, PenPath> TemporaryPaths { get; init; } = new();
        public ImmutableList<PenPath> Paths { get; init; } = ImmutableList<PenPath>.Empty;
        public Color? PaintBrushColor { get; init; }
        public double PaintBrushSize { get; init; }
        public double PaintBrushAlpha { get; init; }
        public double CurrentPenPressure { get; init; }
        public double CurrentZoom { get; init; }
        public bool IsPenDown { get; init; }
        public PaintCanvasTool SelectedTool { get; init; }
        public Size BackgroundImageSize { get; init; }
    }

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonSerializable(typeof(PaintCanvasModel))]
    internal partial class PaintCanvasModelSerializerContext : JsonSerializerContext;
}
