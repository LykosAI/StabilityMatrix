using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
public partial class PaintCanvasViewModel(ILogger<PaintCanvasViewModel> logger) : LoadableViewModelBase
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

    private bool CanExecuteUndo()
    {
        return Paths.Count > 0;
    }

    public SKImage? RenderToWhiteChannelImage()
    {
        using var _ = CodeTimer.StartDebug();

        if (BackgroundImageSize == Size.Empty)
        {
            logger.LogWarning("RenderToImage: Background image size is not set, null.");
            return null;
        }

        using var surface = SKSurface.Create(
            new SKImageInfo(BackgroundImageSize.Width, BackgroundImageSize.Height)
        );
        using var canvas = surface.Canvas;

        RenderToCanvas(canvas);

        using var originalImage = surface.Snapshot();
        // Replace all colors to white (255, 255, 255), keep original alpha
        // csharpier-ignore
        using var colorFilter = SKColorFilter.CreateColorMatrix(
            [
                // R, G, B, A, Bias
                255, 0, 0, 0, 0,
                0, 255, 0, 0, 0,
                0, 0, 255, 0, 0,
                0, 0, 0, 1, 0
            ]
        );

        using var paint = new SKPaint();
        paint.ColorFilter = colorFilter;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(originalImage, originalImage.Info.Rect, paint);

        return surface.Snapshot();
    }

    public SKImage? RenderToImage()
    {
        using var _ = CodeTimer.StartDebug();

        if (BackgroundImageSize == Size.Empty)
        {
            logger.LogWarning("RenderToImage: Background image size is not set, returning null.");
            return null;
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
}
