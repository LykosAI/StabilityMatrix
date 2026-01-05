using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
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
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Services;
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
    private readonly IImageIndexService imageIndexService;
    private int layerCounter;
    private int imageLayerCounter;

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

    /// <summary>
    /// Whether the recent images panel is expanded.
    /// </summary>
    [ObservableProperty]
    private bool isRecentImagesPanelExpanded;

    /// <summary>
    /// Collection of recent inference images for quick selection.
    /// </summary>
    public IObservableCollection<LocalImageFile> LocalImages { get; } =
        new ObservableCollectionExtended<LocalImageFile>();

    public LayeredMaskEditorViewModel(
        IServiceManager<ViewModelBase> vmFactory,
        IImageIndexService imageIndexService
    )
    {
        this.vmFactory = vmFactory;
        this.imageIndexService = imageIndexService;
        PaintCanvasViewModel = vmFactory.Get<PaintCanvasViewModel>();

        // Subscribe to recent images
        imageIndexService
            .InferenceImages.ItemsSource.Connect()
            .DeferUntilLoaded()
            .SortBy(file => file.LastModifiedAt, SortDirection.Descending)
            .Top(50) // Limit to 50 most recent
            .Bind(LocalImages)
            .Subscribe();

        // Initialize with one layer
        AddLayer();
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        // Refresh the image index to populate recent images
        await imageIndexService.RefreshIndexForAllCollections();
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

    /// <summary>
    /// Adds a new image layer on top of the stack.
    /// </summary>
    [RelayCommand]
    private void AddImageLayer()
    {
        imageLayerCounter++;
        var layer = new MaskLayer
        {
            Name = $"Image {imageLayerCounter}",
            LayerType = MaskLayerType.Image,
            DisplayColor = new SKColor(128, 128, 128), // Gray for image layers
        };

        // Subscribe to layer property changes to refresh canvas
        layer.PropertyChanged += Layer_PropertyChanged;

        Layers.Add(layer);
        SelectedLayer = layer;

        // Expand the recent images panel when adding an image layer
        IsRecentImagesPanelExpanded = true;

        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    /// Selects an image from the recent images panel for the current image layer.
    /// </summary>
    [RelayCommand]
    private async Task SelectImageFromRecent(LocalImageFile? imageFile)
    {
        if (imageFile is null || SelectedLayer is null)
            return;

        // If selected layer is not an image layer, create a new one
        if (SelectedLayer.LayerType != MaskLayerType.Image)
        {
            AddImageLayer();
        }

        await LoadImageIntoLayerAsync(SelectedLayer!, imageFile.AbsolutePath);
    }

    /// <summary>
    /// Opens a file picker to select an image for the current image layer.
    /// </summary>
    [RelayCommand]
    private async Task BrowseImageForLayer()
    {
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select Reference Image",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"],
                    },
                ],
            }
        );

        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;

        // If no layer selected or current layer is paint, create a new image layer
        if (SelectedLayer is null || SelectedLayer.LayerType != MaskLayerType.Image)
        {
            AddImageLayer();
        }

        await LoadImageIntoLayerAsync(SelectedLayer!, path);
    }

    /// <summary>
    /// Loads an image from the given path into the specified layer.
    /// </summary>
    private async Task LoadImageIntoLayerAsync(MaskLayer layer, string imagePath)
    {
        if (layer.LayerType != MaskLayerType.Image)
            return;

        try
        {
            // Load bitmap on background thread
            var bitmap = await Task.Run(() =>
            {
                using var stream = File.OpenRead(imagePath);
                return SKBitmap.Decode(stream);
            });

            if (bitmap is null)
                return;

            // Dispose old bitmap
            layer.SourceImage?.Dispose();

            // Store the path and bitmap
            layer.SourceImagePath = imagePath;
            layer.SourceImage = bitmap;

            // Refresh canvas (SourceImage property change also triggers Layer_PropertyChanged)
            SyncSelectedLayerToCanvas();
        }
        catch (Exception)
        {
            // Silently fail - could add notification here
        }
    }

    private void Layer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Refresh canvas when visibility, opacity, or image scale changes
        if (
            e.PropertyName
            is nameof(MaskLayer.IsVisible)
                or nameof(MaskLayer.Opacity)
                or nameof(MaskLayer.ImageScale)
                or nameof(MaskLayer.SourceImage)
        )
        {
            var changedLayer = sender as MaskLayer;

            // Save paths before sync, but handle visibility toggle specially:
            // - When toggling OFF (IsVisible is now false): canvas has paths, SAVE them
            // - When toggling ON (IsVisible is now true): canvas was empty, DON'T save
            if (
                changedLayer == SelectedLayer
                && e.PropertyName == nameof(MaskLayer.IsVisible)
                && changedLayer.IsVisible
            )
            {
                // Toggling ON - skip save (canvas was empty while hidden)
            }
            else
            {
                // All other cases: save paths
                SaveCurrentLayerPaths();
            }

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
    /// Fills the selected layer with a rectangle covering the entire canvas.
    /// </summary>
    [RelayCommand]
    private void FillLayer()
    {
        if (SelectedLayer is null || SelectedLayer.LayerType != MaskLayerType.Paint)
            return;

        // Create a rectangle path covering the entire canvas
        var fillPath = new PenPath
        {
            PathType = PenPathType.Rectangle,
            FillColor = SelectedLayer.DisplayColor,
            Bounds = new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height),
        };

        // Add to current paths
        SelectedLayer.Paths = SelectedLayer.Paths.Add(fillPath);
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    /// Duplicates the selected layer with all its content and settings.
    /// </summary>
    [RelayCommand]
    private void DuplicateLayer()
    {
        if (SelectedLayer is null)
            return;

        // Save current layer paths first
        SaveCurrentLayerPaths();

        layerCounter++;
        var clone = new MaskLayer
        {
            Name = $"{SelectedLayer.Name} Copy",
            LayerType = SelectedLayer.LayerType,
            DisplayColor = SelectedLayer.DisplayColor,
            Prompt = SelectedLayer.Prompt,
            Strength = SelectedLayer.Strength,
            Opacity = SelectedLayer.Opacity,
            IsVisible = SelectedLayer.IsVisible,
            IsEnabled = SelectedLayer.IsEnabled,
            Paths = SelectedLayer.Paths, // ImmutableList, safe to share
            SourceImagePath = SelectedLayer.SourceImagePath,
            ImageScale = SelectedLayer.ImageScale,
        };

        // Subscribe to layer property changes
        clone.PropertyChanged += Layer_PropertyChanged;

        // Insert after current layer
        var index = Layers.IndexOf(SelectedLayer);
        Layers.Insert(index + 1, clone);
        SelectedLayer = clone;
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    /// Called when the selected layer changes.
    /// Saves the current layer's paths and loads the new layer's paths.
    /// </summary>
    partial void OnSelectedLayerChanging(MaskLayer? oldValue, MaskLayer? newValue)
    {
        // Save paths from old layer before switching
        SaveCurrentLayerPaths();
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
    /// Other visible layers are rendered to their correct z-order positions.
    /// </summary>
    private void SyncSelectedLayerToCanvas()
    {
        if (SelectedLayer is null)
        {
            PaintCanvasViewModel.Paths = [];
            PaintCanvasViewModel.SetLayerBitmap("LayersBelow", null);
            PaintCanvasViewModel.SetLayerBitmap("LayersAbove", null);
            PaintCanvasViewModel.RefreshCanvas?.Invoke();
            return;
        }

        // Set brush color to layer's display color for visual feedback
        PaintCanvasViewModel.PaintBrushColor = SelectedLayer.AvaloniaDisplayColor;
        PaintCanvasViewModel.CanvasSize = CanvasSize;

        // Handle different layer types
        if (SelectedLayer.LayerType == MaskLayerType.Image)
        {
            // Image layers are reference-only, disable drawing
            PaintCanvasViewModel.IsDrawingEnabled = false;
            PaintCanvasViewModel.Paths = [];

            // Render the selected image layer's bitmap directly if visible and has content
            if (SelectedLayer.IsVisible && SelectedLayer.SourceImage != null && CanvasSize != Size.Empty)
            {
                var selectedImageBitmap = RenderSingleImageLayer(SelectedLayer);
                PaintCanvasViewModel.SetLayerBitmap("CurrentImage", selectedImageBitmap);
            }
            else
            {
                PaintCanvasViewModel.SetLayerBitmap("CurrentImage", null);
            }
        }
        else if (SelectedLayer.IsVisible)
        {
            // Paint layer - enable drawing and show paths if visible
            PaintCanvasViewModel.IsDrawingEnabled = true;
            PaintCanvasViewModel.Paths = SelectedLayer.Paths;
            PaintCanvasViewModel.SetLayerBitmap("CurrentImage", null);
        }
        else
        {
            // Layer is hidden - still allow drawing but don't render its paths until shown
            PaintCanvasViewModel.IsDrawingEnabled = true;
            PaintCanvasViewModel.Paths = [];
            PaintCanvasViewModel.SetLayerBitmap("CurrentImage", null);
        }

        if (ShowAllLayers && CanvasSize != Size.Empty)
        {
            // Render layers to their correct z-order positions
            var (belowBitmap, aboveBitmap) = RenderLayersByPosition();
            PaintCanvasViewModel.SetLayerBitmap("LayersBelow", belowBitmap);
            PaintCanvasViewModel.SetLayerBitmap("LayersAbove", aboveBitmap);
        }
        else
        {
            // Clear other layer bitmaps
            PaintCanvasViewModel.SetLayerBitmap("LayersBelow", null);
            PaintCanvasViewModel.SetLayerBitmap("LayersAbove", null);
        }

        PaintCanvasViewModel.RefreshCanvas?.Invoke();
    }

    /// <summary>
    /// Renders a single image layer's bitmap at the canvas size with scaling.
    /// </summary>
    private SKBitmap? RenderSingleImageLayer(MaskLayer layer)
    {
        if (layer.SourceImage is null || CanvasSize == Size.Empty)
            return null;

        var bitmap = new SKBitmap(
            CanvasSize.Width,
            CanvasSize.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var alpha = (byte)(layer.Opacity * 255);
        RenderImageLayer(canvas, layer, alpha);

        return bitmap;
    }

    /// <summary>
    /// Renders visible layers split into two bitmaps: layers below and above the selected layer.
    /// This enables proper z-ordering where the selected layer maintains its correct position.
    /// Note: In this layer system, LOWER index = drawn on TOP (like Photoshop's layer panel).
    /// </summary>
    /// <returns>A tuple of (layersBelow, layersAbove) bitmaps. Either may be null if empty.</returns>
    private (SKBitmap? LayersBelow, SKBitmap? LayersAbove) RenderLayersByPosition()
    {
        if (CanvasSize == Size.Empty || SelectedLayer is null)
            return (null, null);

        var selectedIndex = Layers.IndexOf(SelectedLayer);
        if (selectedIndex < 0)
            return (null, null);

        SKBitmap? belowBitmap = null;
        SKBitmap? aboveBitmap = null;

        // Layers with LOWER index than selected = drawn on TOP (rendered to Overlay layer)
        // These are visually "above" the selected layer
        var hasLayersAbove = false;
        for (var i = 0; i < selectedIndex; i++)
        {
            var layer = Layers[i];
            if (layer.IsVisible && LayerHasContent(layer))
            {
                hasLayersAbove = true;
                break;
            }
        }

        if (hasLayersAbove)
        {
            aboveBitmap = new SKBitmap(
                CanvasSize.Width,
                CanvasSize.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul
            );
            using var aboveCanvas = new SKCanvas(aboveBitmap);
            aboveCanvas.Clear(SKColors.Transparent);

            // Render in reverse order so that lower index (top layer) is drawn last (on top)
            for (var i = selectedIndex - 1; i >= 0; i--)
            {
                var layer = Layers[i];
                if (!layer.IsVisible || !LayerHasContent(layer))
                    continue;

                RenderLayerToCanvas(aboveCanvas, layer);
            }
        }

        // Layers with HIGHER index than selected = drawn BELOW (rendered to Images layer)
        // These are visually "behind" the selected layer
        var hasLayersBelow = false;
        for (var i = selectedIndex + 1; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (layer.IsVisible && LayerHasContent(layer))
            {
                hasLayersBelow = true;
                break;
            }
        }

        if (hasLayersBelow)
        {
            belowBitmap = new SKBitmap(
                CanvasSize.Width,
                CanvasSize.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul
            );
            using var belowCanvas = new SKCanvas(belowBitmap);
            belowCanvas.Clear(SKColors.Transparent);

            // Render from bottom to top (highest index first, as it's the bottom-most)
            for (var i = Layers.Count - 1; i > selectedIndex; i--)
            {
                var layer = Layers[i];
                if (!layer.IsVisible || !LayerHasContent(layer))
                    continue;

                RenderLayerToCanvas(belowCanvas, layer);
            }
        }

        return (belowBitmap, aboveBitmap);
    }

    /// <summary>
    /// Checks if a layer has any renderable content.
    /// </summary>
    private static bool LayerHasContent(MaskLayer layer)
    {
        return layer.LayerType == MaskLayerType.Image ? layer.SourceImage != null : layer.Paths.Count > 0;
    }

    /// <summary>
    /// Renders a single layer to a canvas with the layer's settings and opacity.
    /// Handles both paint layers (paths) and image layers (bitmaps).
    /// </summary>
    private void RenderLayerToCanvas(SKCanvas canvas, MaskLayer layer)
    {
        var alpha = (byte)(layer.Opacity * 255);

        if (layer.LayerType == MaskLayerType.Image && layer.SourceImage != null)
        {
            // Render image layer with scaling
            RenderImageLayer(canvas, layer, alpha);
        }
        else
        {
            // Render paint layer paths
            using var paint = new SKPaint
            {
                Color = new SKColor(
                    layer.DisplayColor.Red,
                    layer.DisplayColor.Green,
                    layer.DisplayColor.Blue,
                    alpha
                ),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            foreach (var penPath in layer.Paths)
            {
                RenderPenPathToCanvas(canvas, penPath, paint);
            }
        }
    }

    /// <summary>
    /// Renders an image layer with scaling and centering.
    /// </summary>
    private void RenderImageLayer(SKCanvas canvas, MaskLayer layer, byte alpha)
    {
        if (layer.SourceImage is null)
            return;

        var bitmap = layer.SourceImage;
        var scale = (float)layer.ImageScale;

        // Calculate scaled dimensions
        var scaledWidth = bitmap.Width * scale;
        var scaledHeight = bitmap.Height * scale;

        // Center the image on the canvas
        var offsetX = (CanvasSize.Width - scaledWidth) / 2f;
        var offsetY = (CanvasSize.Height - scaledHeight) / 2f;

        var destRect = new SKRect(offsetX, offsetY, offsetX + scaledWidth, offsetY + scaledHeight);

        using var paint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, alpha),
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
        };

        canvas.DrawBitmap(bitmap, destRect, paint);
    }

    /// <summary>
    /// Saves the current canvas paths back to the selected layer.
    /// Only saves for paint layers that could have been edited.
    /// </summary>
    public void SaveCurrentLayerPaths()
    {
        // Only save for paint layers (image layers don't have editable paths)
        if (SelectedLayer is not null && SelectedLayer.LayerType == MaskLayerType.Paint)
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

        // Draw paths - RenderPenPath will use penPath.FillColor, so we render first
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        foreach (var penPath in layer.Paths)
        {
            RenderPenPathToCanvas(canvas, penPath, paint);
        }

        // Now convert all colors to white, keeping alpha
        // This ensures the mask is white regardless of display color
        using var originalImage = surface.Snapshot();
        // csharpier-ignore
        using var colorFilter = SKColorFilter.CreateColorMatrix(
            [
                // R, G, B, A, Bias
                // Convert any color to white (255, 255, 255), keep original alpha
                0, 0, 0, 0, 255,  // R = 255
                0, 0, 0, 0, 255,  // G = 255
                0, 0, 0, 0, 255,  // B = 255
                0, 0, 0, 1, 0     // A = original alpha
            ]
        );

        using var filterPaint = new SKPaint();
        filterPaint.ColorFilter = colorFilter;

        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(originalImage, originalImage.Info.Rect, filterPaint);

        return surface.Snapshot();
    }

    /// <summary>
    /// Renders a pen path to a canvas. Delegates to PaintCanvasViewModel's shared implementation.
    /// </summary>
    private static void RenderPenPathToCanvas(SKCanvas canvas, PenPath penPath, SKPaint paint)
    {
        PaintCanvasViewModel.RenderPenPath(canvas, penPath, paint);
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
