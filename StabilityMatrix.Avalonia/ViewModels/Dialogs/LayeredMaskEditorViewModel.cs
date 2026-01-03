using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Controls;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using ContentDialogButton = FluentAvalonia.UI.Controls.ContentDialogButton;
using Size = System.Drawing.Size;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the layered mask editor dialog.
/// Manages multiple layers with independent masks, prompts, and opacity settings.
/// </summary>
[RegisterTransient<LayeredMaskEditorViewModel>]
[ManagedService]
[View(typeof(LayeredMaskEditorDialog))]
public partial class LayeredMaskEditorViewModel : LoadableViewModelBase, IDisposable
{
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private int layerCounter;

    /// <summary>
    /// The collection of layers in the editor (ordered from bottom to top).
    /// </summary>
    public ObservableCollection<MaskLayer> Layers { get; } = [];

    /// <summary>
    /// The currently selected layer for editing.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteLayerCommand))]
    private MaskLayer? selectedLayer;

    /// <summary>
    /// Canvas size for all layers.
    /// </summary>
    [ObservableProperty]
    private Size canvasSize = new(1024, 1024);

    /// <summary>
    /// When true, shows all layers composited on the canvas.
    /// When false, shows only the selected layer.
    /// </summary>
    [ObservableProperty]
    private bool showAllLayers = true;

    /// <summary>
    /// The paint canvas view model for the currently selected layer.
    /// </summary>
    public PaintCanvasViewModel PaintCanvasViewModel { get; }

    public LayeredMaskEditorViewModel(IServiceManager<ViewModelBase> vmFactory)
    {
        this.vmFactory = vmFactory;
        PaintCanvasViewModel = vmFactory.Get<PaintCanvasViewModel>();

        // Initialize with one layer
        AddLayer();
    }

    /// <summary>
    /// Adds a new layer on top of the stack.
    /// </summary>
    [RelayCommand]
    private void AddLayer()
    {
        layerCounter++;
        var layer = new MaskLayer
        {
            Name = $"Layer {layerCounter}",
            DisplayColor = MaskLayerColors.GetByIndex(layerCounter - 1),
        };

        // Subscribe to layer property changes to refresh canvas
        layer.PropertyChanged += Layer_PropertyChanged;

        Layers.Add(layer);
        SelectedLayer = layer;
        SyncSelectedLayerToCanvas();
    }

    private void Layer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Refresh canvas when visibility changes
        if (e.PropertyName == nameof(MaskLayer.IsVisible))
        {
            SyncSelectedLayerToCanvas();
        }
    }

    /// <summary>
    /// Refreshes the canvas composite. Call after drawing to update layer order.
    /// </summary>
    public void RefreshComposite()
    {
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    /// Deletes the selected layer.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteLayer))]
    private void DeleteLayer()
    {
        if (SelectedLayer is null)
            return;

        // Save current layer before removing
        SaveCurrentLayerPaths();

        var index = Layers.IndexOf(SelectedLayer);
        Layers.Remove(SelectedLayer);

        // Select adjacent layer
        if (Layers.Count > 0)
        {
            SelectedLayer = Layers[Math.Min(index, Layers.Count - 1)];
            SyncSelectedLayerToCanvas();
        }
        else
        {
            SelectedLayer = null;
            PaintCanvasViewModel.Paths = [];
            PaintCanvasViewModel.RefreshCanvas?.Invoke();
        }
    }

    private bool CanDeleteLayer() => SelectedLayer is not null && Layers.Count > 1;

    /// <summary>
    /// Moves the specified layer (or selected layer if null) up in the list (toward top of list = drawn on TOP).
    /// </summary>
    [RelayCommand]
    private void MoveLayerUp(MaskLayer? layer)
    {
        layer ??= SelectedLayer;
        if (layer is null)
            return;

        var index = Layers.IndexOf(layer);
        if (index > 0)
        {
            Layers.Move(index, index - 1);
            SyncSelectedLayerToCanvas();
        }
    }

    /// <summary>
    /// Moves the specified layer (or selected layer if null) down in the list (toward bottom of list = drawn UNDER others).
    /// </summary>
    [RelayCommand]
    private void MoveLayerDown(MaskLayer? layer)
    {
        layer ??= SelectedLayer;
        if (layer is null)
            return;

        var index = Layers.IndexOf(layer);
        if (index < Layers.Count - 1)
        {
            Layers.Move(index, index + 1);
            SyncSelectedLayerToCanvas();
        }
    }

    /// <summary>
    /// Called when the selected layer changes.
    /// Saves the current layer's paths and loads the new layer's paths.
    /// </summary>
    partial void OnSelectedLayerChanging(MaskLayer? oldValue, MaskLayer? newValue)
    {
        // Save paths from old layer
        if (oldValue is not null)
        {
            oldValue.Paths = PaintCanvasViewModel.Paths;
        }
    }

    partial void OnSelectedLayerChanged(MaskLayer? value)
    {
        SyncSelectedLayerToCanvas();
    }

    partial void OnCanvasSizeChanged(Size value)
    {
        PaintCanvasViewModel.CanvasSize = value;
    }

    partial void OnShowAllLayersChanged(bool value)
    {
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    /// Syncs the selected layer's paths and brush color to the paint canvas.
    /// Other visible layers are rendered as a background image.
    /// </summary>
    private void SyncSelectedLayerToCanvas()
    {
        if (SelectedLayer is null)
        {
            PaintCanvasViewModel.Paths = [];
            PaintCanvasViewModel.RefreshCanvas?.Invoke();
            return;
        }

        // Set brush color to layer's display color for visual feedback
        PaintCanvasViewModel.PaintBrushColor = SelectedLayer.AvaloniaDisplayColor;
        PaintCanvasViewModel.CanvasSize = CanvasSize;

        // Always load only the selected layer's paths for active drawing
        PaintCanvasViewModel.Paths = SelectedLayer.Paths;

        if (ShowAllLayers && CanvasSize != Size.Empty)
        {
            // Render other visible layers as a background image
            var otherLayersBitmap = RenderOtherLayersToBackground();
            PaintCanvasViewModel.SetLayerBitmap("OtherLayers", otherLayersBitmap);
        }
        else
        {
            // Clear the other layers background
            PaintCanvasViewModel.SetLayerBitmap("OtherLayers", null);
        }

        PaintCanvasViewModel.RefreshCanvas?.Invoke();
    }

    /// <summary>
    /// Renders all visible layers EXCEPT the selected layer to a bitmap for background display.
    /// </summary>
    private SKBitmap? RenderOtherLayersToBackground()
    {
        if (CanvasSize == Size.Empty)
            return null;

        var bitmap = new SKBitmap(
            CanvasSize.Width,
            CanvasSize.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Draw layers in order (bottom to top), skipping selected layer
        foreach (var layer in Layers)
        {
            if (!layer.IsVisible || layer == SelectedLayer || layer.Paths.Count == 0)
                continue;

            using var paint = new SKPaint
            {
                Color = new SKColor(
                    layer.DisplayColor.Red,
                    layer.DisplayColor.Green,
                    layer.DisplayColor.Blue
                ),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            foreach (var penPath in layer.Paths)
            {
                RenderPenPathToCanvas(canvas, penPath, paint);
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Saves the current canvas paths back to the selected layer.
    /// </summary>
    public void SaveCurrentLayerPaths()
    {
        if (SelectedLayer is not null)
        {
            SelectedLayer.Paths = PaintCanvasViewModel.Paths;
        }
    }

    /// <summary>
    /// Gets enabled layers with content (for generation).
    /// </summary>
    public IReadOnlyList<MaskLayer> GetEnabledLayersWithContent()
    {
        // Save current layer first
        SaveCurrentLayerPaths();

        return Layers
            .Where(l => l.IsEnabled && l.HasContent && !string.IsNullOrWhiteSpace(l.Prompt))
            .ToList();
    }

    /// <summary>
    /// Renders a specific layer's paths to a white mask image.
    /// </summary>
    public SKImage? RenderLayerToMask(MaskLayer layer)
    {
        if (layer.Paths.Count == 0 || CanvasSize == Size.Empty)
            return null;

        // Create a temporary surface
        using var surface = SKSurface.Create(new SKImageInfo(CanvasSize.Width, CanvasSize.Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Draw paths in white
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        foreach (var penPath in layer.Paths)
        {
            RenderPenPathToCanvas(canvas, penPath, paint);
        }

        return surface.Snapshot();
    }

    private static void RenderPenPathToCanvas(SKCanvas canvas, PenPath penPath, SKPaint paint)
    {
        if (penPath.Points.Count == 0)
            return;

        // Track if we have any pen points
        var hasPenPoints = false;

        for (var i = 0; i < penPath.Points.Count; i++)
        {
            var penPoint = penPath.Points[i];
            if (!penPoint.IsPen)
                continue;

            hasPenPoints = true;
            var radius = penPoint.Radius;
            var pressure = penPoint.Pressure ?? 1;
            var thickness = pressure * radius * 2.5;

            // Draw path segments
            if (i < penPath.Points.Count - 1)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = (float)thickness;
                paint.StrokeCap = SKStrokeCap.Round;
                paint.StrokeJoin = SKStrokeJoin.Round;

                var nextPoint = penPath.Points[i + 1];
                canvas.DrawLine(penPoint.X, penPoint.Y, nextPoint.X, nextPoint.Y, paint);
            }

            // Draw circles for pen points
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(penPoint.X, penPoint.Y, (float)thickness / 2, paint);
        }

        // Draw paths directly if no pen points
        if (!hasPenPoints && penPath.Points.Count > 0)
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

    /// <summary>
    /// Gets the dialog for this view model.
    /// </summary>
    public BetterContentDialog GetDialog()
    {
        Dispatcher.UIThread.VerifyAccess();

        var dialog = new BetterContentDialog
        {
            Content = this,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxDialogHeight = 2000,
            MaxDialogWidth = 2500,
            ContentMargin = new Thickness(16),
            FullSizeDesired = true,
            PrimaryButtonText = Resources.Action_Save,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };

        return dialog;
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        base.LoadStateFromJsonObject(state);

        // Load canvas size
        if (
            state.TryGetPropertyValue("canvasWidth", out var widthNode)
            && state.TryGetPropertyValue("canvasHeight", out var heightNode)
        )
        {
            var width = widthNode?.GetValue<int>() ?? 1024;
            var height = heightNode?.GetValue<int>() ?? 1024;
            CanvasSize = new Size(width, height);
        }

        // Load layers
        if (state.TryGetPropertyValue("layers", out var layersNode) && layersNode is JsonArray layersArray)
        {
            Layers.Clear();
            layerCounter = 0;

            foreach (var layerNode in layersArray)
            {
                if (layerNode is JsonObject layerObj)
                {
                    var layer = new MaskLayer();
                    layer.LoadStateFromJsonObject(layerObj);
                    Layers.Add(layer);
                    layerCounter++;
                }
            }

            // Select first layer
            if (Layers.Count > 0)
            {
                SelectedLayer = Layers[0];
            }
        }

        // Ensure at least one layer exists
        if (Layers.Count == 0)
        {
            AddLayer();
        }
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        // Save current layer paths first
        SaveCurrentLayerPaths();

        var state = base.SaveStateToJsonObject();

        // Save canvas size
        state["canvasWidth"] = CanvasSize.Width;
        state["canvasHeight"] = CanvasSize.Height;

        // Save layers
        var layersArray = new JsonArray();
        foreach (var layer in Layers)
        {
            layersArray.Add(layer.SaveStateToJsonObject());
        }
        state["layers"] = layersArray;

        return state;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
