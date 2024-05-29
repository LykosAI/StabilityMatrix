using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using Color = Avalonia.Media.Color;
using Size = System.Drawing.Size;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Controls;

[Transient]
[ManagedService]
public partial class PaintCanvasViewModel : LoadableViewModelBase
{
    public ConcurrentDictionary<long, PenPath> TemporaryPaths { get; set; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private ImmutableList<PenPath> paths = [];

    [ObservableProperty]
    private Color? paintBrushColor = Colors.White;

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
    private PaintCanvasTool selectedTool = PaintCanvasTool.PaintBrush;

    [ObservableProperty]
    [property: JsonIgnore]
    private SKBitmap? backgroundImage;

    [ObservableProperty]
    private Size backgroundImageSize = Size.Empty;

    [JsonIgnore]
    public List<SKBitmap> LayerImages { get; } = [];

    /// <summary>
    /// Set by <see cref="PaintCanvas"/> to allow the view model to
    /// refresh the canvas view after updating points or bitmap layers.
    /// </summary>
    [JsonIgnore]
    public Action? RefreshCanvas { get; set; }

    partial void OnBackgroundImageChanged(SKBitmap? value)
    {
        // Set the size of the background image
        if (value is not null)
        {
            BackgroundImageSize = new Size(value.Width, value.Height);
        }
    }

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

    private static void RenderPenPath(SKCanvas canvas, PenPath penPath, SKPaint paint)
    {
        if (penPath.Points.Count == 0)
        {
            return;
        }

        // Apply Color
        paint.Color = penPath.FillColor;

        if (penPath.IsErase)
        {
            paint.BlendMode = SKBlendMode.SrcIn;
            paint.Color = SKColors.Transparent;
        }

        // Defaults
        paint.IsDither = true;
        paint.IsAntialias = true;

        // Track if we have any pen points
        var hasPenPoints = false;

        // Can't use foreach since this list may be modified during iteration
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < penPath.Points.Count; i++)
        {
            var penPoint = penPath.Points[i];

            // Skip non-pen points
            if (!penPoint.IsPen)
            {
                continue;
            }

            hasPenPoints = true;

            var radius = penPoint.Radius;
            var pressure = penPoint.Pressure ?? 1;
            var thickness = pressure * radius * 2.5;

            // Draw path
            if (i < penPath.Points.Count - 1)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = (float)thickness;
                paint.StrokeCap = SKStrokeCap.Round;
                paint.StrokeJoin = SKStrokeJoin.Round;

                var nextPoint = penPath.Points[i + 1];
                canvas.DrawLine(penPoint.X, penPoint.Y, nextPoint.X, nextPoint.Y, paint);
            }

            // Draw circles for pens
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(penPoint.X, penPoint.Y, (float)thickness / 2, paint);
        }

        // Draw paths directly if we didn't have any pen points
        if (!hasPenPoints)
        {
            var point = penPath.Points[0];
            var thickness = point.Radius * 2;

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = (float)thickness;
            paint.StrokeCap = SKStrokeCap.Round;
            paint.StrokeJoin = SKStrokeJoin.Round;

            var skPath = penPath.ToSKPath();
            canvas.DrawPath(skPath, paint);
        }
    }

    public SKImage RenderToImage()
    {
        using var _ = CodeTimer.StartDebug();

        if (BackgroundImageSize == Size.Empty)
        {
            throw new InvalidOperationException("Background image size is not set");
        }

        using var surface = SKSurface.Create(
            new SKImageInfo(BackgroundImageSize.Width, BackgroundImageSize.Height)
        );
        using var canvas = surface.Canvas;

        RenderToCanvas(canvas);

        return surface.Snapshot();
    }

    public void RenderToCanvas(
        SKCanvas canvas,
        bool renderBackgroundFill = false,
        bool renderBackgroundImage = false
    )
    {
        // Draw background color
        canvas.Clear(SKColors.Transparent);

        // Draw background image if set
        if (renderBackgroundImage && BackgroundImage is not null)
        {
            canvas.DrawBitmap(BackgroundImage, new SKPoint(0, 0));
        }

        // Draw any additional images
        foreach (var layerImage in LayerImages)
        {
            canvas.DrawBitmap(layerImage, new SKPoint(0, 0));
        }

        using var paint = new SKPaint();

        // Draw the paths
        foreach (var penPath in TemporaryPaths.Values)
        {
            RenderPenPath(canvas, penPath, paint);
        }

        foreach (var penPath in Paths)
        {
            RenderPenPath(canvas, penPath, paint);
        }

        canvas.Flush();
    }

    public override JsonObject SaveStateToJsonObject()
    {
        var model = SaveCanvas();

        return JsonSerializer
                .SerializeToNode(model, PaintCanvasModelSerializerContext.Default.Options)
                ?.AsObject() ?? throw new InvalidOperationException();
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        // base.LoadStateFromJsonObject(state);

        var model = state.Deserialize<PaintCanvasModel>(PaintCanvasModelSerializerContext.Default.Options);

        if (model is null)
            return;

        LoadCanvas(model);

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
            PaintBrushAlpha = PaintBrushAlpha,
            CurrentPenPressure = CurrentPenPressure,
            CurrentZoom = CurrentZoom,
            IsPenDown = IsPenDown,
            SelectedTool = SelectedTool,
            BackgroundImageSize = BackgroundImageSize
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
