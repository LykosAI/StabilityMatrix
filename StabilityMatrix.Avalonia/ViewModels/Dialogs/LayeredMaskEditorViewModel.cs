using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
///     ViewModel for the layered mask editor dialog.
///     Manages multiple layers with independent masks, prompts, and opacity settings.
/// </summary>
[RegisterTransient<LayeredMaskEditorViewModel>]
[ManagedService]
[View(typeof(LayeredMaskEditorDialog))]
public partial class LayeredMaskEditorViewModel : LoadableViewModelBase, IDisposable
{
    private readonly IImageIndexService imageIndexService;
    private readonly IServiceManager<ViewModelBase> vmFactory;

    /// <summary>
    ///     Canvas size for all layers.
    /// </summary>
    [ObservableProperty]
    private Size canvasSize = new(1024, 1024);

    /// <summary>
    ///     Previous canvas size, used for rescaling layers when dimensions change.
    /// </summary>
    private Size _previousCanvasSize = new(1024, 1024);

    private int imageLayerCounter;

    /// <summary>
    ///     Whether the recent images panel is expanded.
    /// </summary>
    [ObservableProperty]
    private bool isRecentImagesPanelExpanded;

    private int layerCounter;

    /// <summary>
    ///     Cached bitmap for the currently selected image layer.
    ///     Invalidated when source image, scale, opacity, or canvas size changes.
    /// </summary>
    private SKBitmap? _cachedImageLayerBitmap;
    private MaskLayer? _cachedImageLayerSource;
    private SKBitmap? _cachedImageLayerSourceImage;
    private double _cachedImageLayerScale;
    private double _cachedImageLayerOpacity;
    private Size _cachedImageLayerCanvasSize;

    /// <summary>
    ///     The currently selected layer for editing.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteLayerCommand))]
    private MaskLayer? selectedLayer;

    /// <summary>
    ///     When true, shows all layers composited on the canvas.
    ///     When false, shows only the selected layer.
    /// </summary>
    [ObservableProperty]
    private bool showAllLayers = true;

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

    /// <summary>
    ///     The collection of layers in the editor (ordered from bottom to top).
    /// </summary>
    public ObservableCollection<MaskLayer> Layers { get; } = [];

    /// <summary>
    ///     The paint canvas view model for the currently selected layer.
    /// </summary>
    public PaintCanvasViewModel PaintCanvasViewModel { get; }

    /// <summary>
    ///     Collection of recent inference images for quick selection.
    /// </summary>
    public IObservableCollection<LocalImageFile> LocalImages { get; } =
        new ObservableCollectionExtended<LocalImageFile>();

    /// <inheritdoc />
    public void Dispose()
    {
        // Clean up all layers
        foreach (var layer in Layers)
            CleanupLayer(layer);
        Layers.Clear();

        // Dispose cached image layer bitmap
        _cachedImageLayerBitmap?.Dispose();
        _cachedImageLayerBitmap = null;
        _cachedImageLayerSource = null;
        _cachedImageLayerSourceImage = null;

        // Dispose the paint canvas view model
        PaintCanvasViewModel.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        // Refresh the image index to populate recent images
        await imageIndexService.RefreshIndexForAllCollections();
    }

    /// <summary>
    ///     Adds a new layer on top of the stack.
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
    ///     Adds a new image layer on top of the stack.
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
    ///     Selects an image from the recent images panel for the current image layer.
    /// </summary>
    [RelayCommand]
    private async Task SelectImageFromRecent(LocalImageFile? imageFile)
    {
        if (imageFile is null || SelectedLayer is null)
            return;

        // If selected layer is not an image layer, create a new one
        if (SelectedLayer.LayerType != MaskLayerType.Image)
            AddImageLayer();

        await LoadImageIntoLayerAsync(SelectedLayer!, imageFile.AbsolutePath);
    }

    /// <summary>
    ///     Opens a file picker to select an image for the current image layer.
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
            AddImageLayer();

        await LoadImageIntoLayerAsync(SelectedLayer!, path);
    }

    /// <summary>
    ///     Loads an image from the given path into the specified layer.
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

    private void Layer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var changedLayer = sender as MaskLayer;

        // Handle color change: save canvas paths first, recolor them, update brush
        if (e.PropertyName == nameof(MaskLayer.DisplayColor) && changedLayer == SelectedLayer)
        {
            // Save current canvas paths to layer first (so we don't lose any strokes)
            SaveCurrentLayerPaths();

            // Recolor the saved paths with the new color
            if (changedLayer?.Paths.Count > 0)
            {
                var newColor = changedLayer.DisplayColor;
                var recoloredPaths = changedLayer
                    .Paths.Select(p =>
                        p.IsErase ? p : p with { FillColor = newColor.WithAlpha(p.FillColor.Alpha) }
                    )
                    .ToImmutableList();
                changedLayer.Paths = recoloredPaths;
            }

            // Update brush color for new strokes
            PaintCanvasViewModel.PaintBrushColor = changedLayer?.AvaloniaDisplayColor;

            // Sync the recolored paths back to canvas
            SyncSelectedLayerToCanvas();
            return;
        }

        // Refresh canvas when visibility, opacity, paths, lock, or image scale changes
        if (
            e.PropertyName
            is nameof(MaskLayer.IsVisible)
                or nameof(MaskLayer.Opacity)
                or nameof(MaskLayer.ImageScale)
                or nameof(MaskLayer.SourceImage)
                or nameof(MaskLayer.Paths)
                or nameof(MaskLayer.IsLocked)
        )
        {
            // Save paths before sync, but handle visibility toggle specially:
            // - When toggling OFF (IsVisible is now false): canvas has paths, SAVE them
            // - When toggling ON (IsVisible is now true): canvas was empty, DON'T save
            // - For color changes: skip save since MaskLayer itself updates Paths
            if (
                changedLayer == SelectedLayer
                && e.PropertyName == nameof(MaskLayer.IsVisible)
                && changedLayer!.IsVisible
            )
            {
                // Toggling ON - skip save (canvas was empty while hidden)
            }
            else if (e.PropertyName == nameof(MaskLayer.Paths))
            {
                // Paths change from layer update (e.g., other layer's color change), just refresh
            }
            else
            {
                // All other cases: save paths
                // Force save if we are toggling off the selected layer (it's hidden now, but canvas has valid paths)
                var force = changedLayer == SelectedLayer && e.PropertyName == nameof(MaskLayer.IsVisible);
                SaveCurrentLayerPaths(force);
            }

            SyncSelectedLayerToCanvas();
        }
    }

    /// <summary>
    ///     Refreshes the canvas composite. Call after drawing to update layer order.
    /// </summary>
    public void RefreshComposite()
    {
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    ///     Clears the content (paths) of the specified layer without deleting it.
    /// </summary>
    [RelayCommand]
    private void ClearLayerContent(MaskLayer? layer)
    {
        layer ??= SelectedLayer;
        if (layer is null || layer.LayerType == MaskLayerType.Image)
            return;

        // If this is the selected layer, clear the canvas paths too
        if (layer == SelectedLayer)
            PaintCanvasViewModel.Paths = [];

        layer.Paths = [];
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    ///     Deletes the selected layer.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteLayer))]
    private void DeleteLayer()
    {
        if (SelectedLayer is null)
            return;

        // Save current layer before removing
        SaveCurrentLayerPaths();

        var layerToRemove = SelectedLayer;
        var index = Layers.IndexOf(layerToRemove);

        // Unsubscribe and dispose before removing
        CleanupLayer(layerToRemove);
        Layers.Remove(layerToRemove);

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

    /// <summary>
    ///     Unsubscribes event handlers and disposes resources for a layer.
    /// </summary>
    private void CleanupLayer(MaskLayer layer)
    {
        layer.PropertyChanged -= Layer_PropertyChanged;
        layer.SourceImage?.Dispose();
    }

    private bool CanDeleteLayer()
    {
        return SelectedLayer is not null && Layers.Count > 1;
    }

    /// <summary>
    ///     Moves the specified layer (or selected layer if null) up in the list (toward top of list = drawn on TOP).
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
    ///     Moves the specified layer (or selected layer if null) down in the list (toward bottom of list = drawn UNDER
    ///     others).
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
    ///     Handles layer index changes from drag-drop reordering in the UI.
    ///     Called by the View when a layer is dropped at a new position.
    /// </summary>
    /// <param name="layer">The layer that was moved.</param>
    /// <param name="newIndex">The new index where the layer was dropped.</param>
    public void OnLayerIndexChanged(MaskLayer layer, int newIndex)
    {
        var currentIndex = Layers.IndexOf(layer);
        if (currentIndex < 0 || currentIndex == newIndex)
            return;

        // Save current layer paths before moving
        SaveCurrentLayerPaths();

        // Move the layer to the new position
        Layers.Move(currentIndex, newIndex);

        // Refresh the canvas to reflect the new layer order
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    ///     Fills the selected layer with a rectangle covering the entire canvas.
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

    #region Quick Division Presets

    /// <summary>
    ///     Creates layers for a quick division preset.
    ///     Preserves prompts and settings from existing layers where possible.
    /// </summary>
    /// <param name="divisions">Array of (left, top, right, bottom) fractions (0.0-1.0) for each region.</param>
    /// <param name="names">Optional names for each region layer.</param>
    private void CreateQuickDivisionLayers(SKRect[] divisions, string[]? names = null)
    {
        if (CanvasSize == Size.Empty)
            return;

        // Save current layer paths before modifying
        SaveCurrentLayerPaths();

        // Always capture existing layer settings so we can preserve them
        // This preserves prompts when going to more layers, equal layers, or fewer layers
        var existingSettings = Layers
            .Select(l => (l.Prompt, l.NegativePrompt, l.Strength, l.ConditioningArea, l.Opacity, l.IsEnabled))
            .ToList();

        // Clear existing layers
        SelectedLayer = null;
        foreach (var layer in Layers)
            CleanupLayer(layer);
        Layers.Clear();
        layerCounter = 0;

        // Create new layers for each division
        for (var i = 0; i < divisions.Length; i++)
        {
            layerCounter++;
            var layer = new MaskLayer
            {
                Name = names != null && i < names.Length ? names[i] : $"Region {layerCounter}",
                DisplayColor = MaskLayerColors.GetByIndex(layerCounter - 1),
            };

            // Restore settings from existing layers if available
            // This preserves prompts from the first N existing layers
            if (i < existingSettings.Count)
            {
                var settings = existingSettings[i];
                layer.Prompt = settings.Prompt;
                layer.NegativePrompt = settings.NegativePrompt;
                layer.Strength = settings.Strength;
                layer.ConditioningArea = settings.ConditioningArea;
                layer.Opacity = settings.Opacity;
                layer.IsEnabled = settings.IsEnabled;
            }

            // Calculate the actual pixel bounds from fractions
            var fractionalRect = divisions[i];
            var pixelRect = new SKRect(
                fractionalRect.Left * CanvasSize.Width,
                fractionalRect.Top * CanvasSize.Height,
                fractionalRect.Right * CanvasSize.Width,
                fractionalRect.Bottom * CanvasSize.Height
            );

            // Create a filled rectangle for this region
            var fillPath = new PenPath
            {
                PathType = PenPathType.Rectangle,
                FillColor = layer.DisplayColor,
                Bounds = pixelRect,
            };
            layer.Paths = layer.Paths.Add(fillPath);

            // Subscribe to layer property changes
            layer.PropertyChanged += Layer_PropertyChanged;

            Layers.Add(layer);
        }

        // Select the first layer
        if (Layers.Count > 0)
            SelectedLayer = Layers[0];

        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    ///     Rescales all layer paths from oldSize to newSize coordinates.
    /// </summary>
    private void RescaleAllLayersInternal(Size oldSize, Size newSize)
    {
        if (oldSize == newSize || oldSize.Width <= 0 || oldSize.Height <= 0)
            return;

        // Save current layer before rescaling
        SaveCurrentLayerPaths();

        var scaleX = (float)newSize.Width / oldSize.Width;
        var scaleY = (float)newSize.Height / oldSize.Height;

        foreach (var layer in Layers)
        {
            if (layer.LayerType != MaskLayerType.Paint || layer.Paths.Count == 0)
                continue;

            var scaledPaths = layer
                .Paths.Select(path => ScalePenPath(path, scaleX, scaleY))
                .ToImmutableList();
            layer.Paths = scaledPaths;
        }

        // Refresh the canvas to show rescaled paths
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    ///     Scales a PenPath by the given factors.
    /// </summary>
    private static PenPath ScalePenPath(PenPath path, float scaleX, float scaleY)
    {
        // Scale the bounds
        var scaledBounds = new SKRect(
            path.Bounds.Left * scaleX,
            path.Bounds.Top * scaleY,
            path.Bounds.Right * scaleX,
            path.Bounds.Bottom * scaleY
        );

        // Scale points if present (for freehand paths)
        var scaledPoints =
            path.Points.Count > 0
                ? path
                    .Points.Select(p => new PenPoint(p.X * scaleX, p.Y * scaleY)
                    {
                        Pressure = p.Pressure,
                        IsPen = p.IsPen,
                        Radius = p.Radius * Math.Max(scaleX, scaleY), // Scale radius too
                    })
                    .ToList()
                : path.Points;

        // Scale the stroke radius proportionally
        var scaledRadius = path.Radius * Math.Max(scaleX, scaleY);
        var scaledStrokeWidth = path.StrokeWidth * Math.Max(scaleX, scaleY);

        return path with
        {
            Bounds = scaledBounds,
            Points = scaledPoints,
            Radius = (float)scaledRadius,
            StrokeWidth = (float)scaledStrokeWidth,
        };
    }

    /// <summary>
    ///     Manually rescales all layers to fit the current canvas size.
    ///     Useful when layers were created at a different resolution.
    /// </summary>
    [RelayCommand]
    private void RescaleAllLayers()
    {
        if (
            _previousCanvasSize != CanvasSize
            && _previousCanvasSize.Width > 0
            && _previousCanvasSize.Height > 0
        )
        {
            RescaleAllLayersInternal(_previousCanvasSize, CanvasSize);
            _previousCanvasSize = CanvasSize;
        }
    }

    /// <summary>
    ///     Quick preset: 50/50 horizontal split (left and right halves).
    /// </summary>
    [RelayCommand]
    private void QuickDivisionHorizontal5050()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0f, 0f, 0.5f, 1f), // Left half
                new SKRect(0.5f, 0f, 1f, 1f), // Right half
            ],
            ["Left", "Right"]
        );
    }

    /// <summary>
    ///     Quick preset: 33/33/33 horizontal split (thirds).
    /// </summary>
    [RelayCommand]
    private void QuickDivisionHorizontal333333()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0f, 0f, 0.333f, 1f), // Left third
                new SKRect(0.333f, 0f, 0.666f, 1f), // Middle third
                new SKRect(0.666f, 0f, 1f, 1f), // Right third
            ],
            ["Left", "Center", "Right"]
        );
    }

    /// <summary>
    ///     Quick preset: 50/50 vertical split (top and bottom halves).
    /// </summary>
    [RelayCommand]
    private void QuickDivisionVertical5050()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0f, 0f, 1f, 0.5f), // Top half
                new SKRect(0f, 0.5f, 1f, 1f), // Bottom half
            ],
            ["Top", "Bottom"]
        );
    }

    /// <summary>
    ///     Quick preset: 33/33/33 vertical split (thirds).
    /// </summary>
    [RelayCommand]
    private void QuickDivisionVertical333333()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0f, 0f, 1f, 0.333f), // Top third
                new SKRect(0f, 0.333f, 1f, 0.666f), // Middle third
                new SKRect(0f, 0.666f, 1f, 1f), // Bottom third
            ],
            ["Top", "Middle", "Bottom"]
        );
    }

    /// <summary>
    ///     Quick preset: 2x2 quadrants.
    /// </summary>
    [RelayCommand]
    private void QuickDivisionQuadrants()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0f, 0f, 0.5f, 0.5f), // Top-left
                new SKRect(0.5f, 0f, 1f, 0.5f), // Top-right
                new SKRect(0f, 0.5f, 0.5f, 1f), // Bottom-left
                new SKRect(0.5f, 0.5f, 1f, 1f), // Bottom-right
            ],
            ["Top-Left", "Top-Right", "Bottom-Left", "Bottom-Right"]
        );
    }

    /// <summary>
    ///     Quick preset: 3x3 grid (9 regions).
    /// </summary>
    [RelayCommand]
    private void QuickDivision3x3Grid()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0f, 0f, 0.333f, 0.333f), // Top-left
                new SKRect(0.333f, 0f, 0.666f, 0.333f), // Top-center
                new SKRect(0.666f, 0f, 1f, 0.333f), // Top-right
                new SKRect(0f, 0.333f, 0.333f, 0.666f), // Middle-left
                new SKRect(0.333f, 0.333f, 0.666f, 0.666f), // Center
                new SKRect(0.666f, 0.333f, 1f, 0.666f), // Middle-right
                new SKRect(0f, 0.666f, 0.333f, 1f), // Bottom-left
                new SKRect(0.333f, 0.666f, 0.666f, 1f), // Bottom-center
                new SKRect(0.666f, 0.666f, 1f, 1f), // Bottom-right
            ],
            [
                "Top-Left",
                "Top-Center",
                "Top-Right",
                "Middle-Left",
                "Center",
                "Middle-Right",
                "Bottom-Left",
                "Bottom-Center",
                "Bottom-Right",
            ]
        );
    }

    /// <summary>
    ///     Quick preset: Center focus (center region with surrounding frame).
    /// </summary>
    [RelayCommand]
    private void QuickDivisionCenterFocus()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0.25f, 0.25f, 0.75f, 0.75f), // Center (50% of canvas)
                new SKRect(0f, 0f, 1f, 0.25f), // Top strip
                new SKRect(0f, 0.75f, 1f, 1f), // Bottom strip
                new SKRect(0f, 0.25f, 0.25f, 0.75f), // Left strip
                new SKRect(0.75f, 0.25f, 1f, 0.75f), // Right strip
            ],
            ["Center", "Top", "Bottom", "Left", "Right"]
        );
    }

    /// <summary>
    ///     Quick preset: Portrait mode (foreground subject with background).
    ///     Creates a large center oval-ish region and a background region.
    /// </summary>
    [RelayCommand]
    private void QuickDivisionPortrait()
    {
        // For portrait, we create a center region (roughly where a person would be)
        // and a background region
        CreateQuickDivisionLayers(
            [
                new SKRect(0.15f, 0.05f, 0.85f, 0.95f), // Foreground (subject area)
                new SKRect(0f, 0f, 1f, 1f), // Background (full canvas, will be behind)
            ],
            ["Subject", "Background"]
        );
    }

    /// <summary>
    ///     Quick preset: Landscape scene (sky, horizon, ground).
    /// </summary>
    [RelayCommand]
    private void QuickDivisionLandscape()
    {
        CreateQuickDivisionLayers(
            [
                new SKRect(0f, 0f, 1f, 0.35f), // Sky
                new SKRect(0f, 0.35f, 1f, 0.65f), // Horizon/middle ground
                new SKRect(0f, 0.65f, 1f, 1f), // Foreground
            ],
            ["Sky", "Horizon", "Foreground"]
        );
    }

    #endregion

    /// <summary>
    ///     Duplicates the selected layer with all its content and settings.
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
    ///     Exports the selected layer as a white-on-black mask PNG.
    /// </summary>
    [RelayCommand]
    private async Task ExportLayerAsMaskAsync(MaskLayer? layer)
    {
        layer ??= SelectedLayer;
        if (layer is null || layer.LayerType != MaskLayerType.Paint || CanvasSize == Size.Empty)
            return;

        // Save current layer paths before rendering
        if (layer == SelectedLayer)
            SaveCurrentLayerPaths();

        var storageProvider = App.StorageProvider;

        var file = await storageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export Mask as PNG",
                SuggestedFileName = $"{layer.Name}_mask.png",
                FileTypeChoices = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }],
            }
        );

        if (file is null)
            return;

        // Render layer to white-on-black mask
        using var bitmap = new SKBitmap(
            CanvasSize.Width,
            CanvasSize.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);

        // Render paths as white
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        foreach (var penPath in layer.Paths)
            RenderPenPathToCanvas(canvas, penPath, paint, SKColors.White);

        // Save to file
        await using var stream = await file.OpenWriteAsync();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
    }

    /// <summary>
    ///     Imports a mask image as a new layer, converting white areas to the new layer's color.
    /// </summary>
    [RelayCommand]
    private async Task ImportMaskAsLayerAsync()
    {
        if (CanvasSize == Size.Empty)
            return;

        var storageProvider = App.StorageProvider;

        var files = await storageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import Mask Image",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"],
                    },
                ],
            }
        );

        if (files.Count == 0)
            return;

        var file = files[0];
        await using var stream = await file.OpenReadAsync();
        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap is null)
            return;

        // Create new paint layer
        var newLayer = new MaskLayer
        {
            Name = $"Imported Mask {Layers.Count + 1}",
            LayerType = MaskLayerType.Paint,
            DisplayColor = MaskLayerColors.GetByIndex(Layers.Count),
        };
        newLayer.PropertyChanged += Layer_PropertyChanged;

        // Scale bitmap to canvas size and create a fill path
        // For mask import, we create a bitmap path that covers the canvas
        var scaledBitmap = new SKBitmap(
            CanvasSize.Width,
            CanvasSize.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var scaleCanvas = new SKCanvas(scaledBitmap);
        scaleCanvas.Clear(SKColors.Transparent);

        var srcRect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
        var destRect = new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height);
        scaleCanvas.DrawBitmap(bitmap, srcRect, destRect);

        // Convert white areas to layer color using the mask as a bitmap path
        var maskPath = new PenPath
        {
            PathType = PenPathType.Bitmap,
            FillColor = newLayer.DisplayColor,
            Bounds = destRect,
            BitmapData = scaledBitmap,
        };

        newLayer.Paths = [maskPath];
        Layers.Insert(0, newLayer);
        SelectedLayer = newLayer;
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    ///     Called when the selected layer changes.
    ///     Saves the current layer's paths and loads the new layer's paths.
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

    partial void OnCanvasSizeChanged(Size oldValue, Size newValue)
    {
        PaintCanvasViewModel.CanvasSize = newValue;

        // Invalidate cached image layer bitmap since canvas size changed
        _cachedImageLayerBitmap?.Dispose();
        _cachedImageLayerBitmap = null;

        // Rescale all layers if we have a valid previous size and new size
        if (oldValue.Width > 0 && oldValue.Height > 0 && newValue.Width > 0 && newValue.Height > 0)
        {
            RescaleAllLayersInternal(oldValue, newValue);
        }

        _previousCanvasSize = newValue;
    }

    partial void OnShowAllLayersChanged(bool value)
    {
        SyncSelectedLayerToCanvas();
    }

    /// <summary>
    ///     Syncs the selected layer's paths and brush color to the paint canvas.
    ///     Other visible layers are rendered to their correct z-order positions.
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
                // Clone the cached bitmap since SetLayerBitmap will dispose it later
                var bitmapToSet = selectedImageBitmap?.Copy();
                PaintCanvasViewModel.SetLayerBitmap("CurrentImage", bitmapToSet);
            }
            else
            {
                PaintCanvasViewModel.SetLayerBitmap("CurrentImage", null);
            }
        }
        else if (SelectedLayer.IsVisible)
        {
            // Paint layer - enable drawing if not locked, show paths if visible
            PaintCanvasViewModel.IsDrawingEnabled = !SelectedLayer.IsLocked;
            PaintCanvasViewModel.Paths = SelectedLayer.Paths;
            PaintCanvasViewModel.SetLayerBitmap("CurrentImage", null);
        }
        else
        {
            // Layer is hidden - still allow drawing if not locked but don't render its paths until shown
            PaintCanvasViewModel.IsDrawingEnabled = !SelectedLayer.IsLocked;
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
    ///     Renders a single image layer's bitmap at the canvas size with scaling.
    ///     Uses caching to avoid re-rendering on every sync when the image hasn't changed.
    /// </summary>
    /// <returns>
    ///     The cached or newly rendered bitmap. Note: The caller should NOT dispose this bitmap
    ///     as it is managed by the cache. Returns null if no image is available.
    /// </returns>
    private SKBitmap? RenderSingleImageLayer(MaskLayer layer)
    {
        if (layer.SourceImage is null || CanvasSize == Size.Empty)
            return null;

        // Check if we can use the cached bitmap
        if (
            _cachedImageLayerBitmap is not null
            && _cachedImageLayerSource == layer
            && _cachedImageLayerSourceImage == layer.SourceImage
            && Math.Abs(_cachedImageLayerScale - layer.ImageScale) < 0.001
            && Math.Abs(_cachedImageLayerOpacity - layer.Opacity) < 0.001
            && _cachedImageLayerCanvasSize == CanvasSize
        )
        {
            return _cachedImageLayerBitmap;
        }

        // Dispose old cached bitmap
        _cachedImageLayerBitmap?.Dispose();

        // Create new bitmap
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

        // Update cache
        _cachedImageLayerBitmap = bitmap;
        _cachedImageLayerSource = layer;
        _cachedImageLayerSourceImage = layer.SourceImage;
        _cachedImageLayerScale = layer.ImageScale;
        _cachedImageLayerOpacity = layer.Opacity;
        _cachedImageLayerCanvasSize = CanvasSize;

        return bitmap;
    }

    /// <summary>
    ///     Renders visible layers split into two bitmaps: layers below and above the selected layer.
    ///     This enables proper z-ordering where the selected layer maintains its correct position.
    ///     Note: In this layer system, LOWER index = drawn on TOP (like Photoshop's layer panel).
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
    ///     Checks if a layer has any renderable content.
    /// </summary>
    private static bool LayerHasContent(MaskLayer layer)
    {
        return layer.LayerType == MaskLayerType.Image ? layer.SourceImage != null : layer.Paths.Count > 0;
    }

    /// <summary>
    ///     Renders a single layer to a canvas with the layer's settings and opacity.
    ///     Handles both paint layers (paths) and image layers (bitmaps).
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
                RenderPenPathToCanvas(canvas, penPath, paint);
        }
    }

    /// <summary>
    ///     Renders an image layer with scaling and centering.
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

        using var paint = new SKPaint();
        paint.Color = new SKColor(255, 255, 255, alpha);
        paint.IsAntialias = true;
        paint.FilterQuality = SKFilterQuality.High;

        canvas.DrawBitmap(bitmap, destRect, paint);
    }

    /// <summary>
    ///     Saves the current canvas paths back to the selected layer.
    ///     Only saves for paint layers that could have been edited.
    /// </summary>
    public void SaveCurrentLayerPaths(bool force = false)
    {
        // Only save for paint layers (image layers don't have editable paths)
        if (SelectedLayer is null || SelectedLayer.LayerType != MaskLayerType.Paint)
            return;

        // If the layer is hidden, PaintCanvasViewModel.Paths is cleared (visually hidden)
        // by SyncSelectedLayerToCanvas. We should not overwrite the layer's actual paths
        // with this empty list. This prevents data loss when moving/updating hidden layers.
        if (!force && !SelectedLayer.IsVisible)
            return;

        SelectedLayer.Paths = PaintCanvasViewModel.Paths;
    }

    /// <summary>
    ///     Gets enabled layers with content (for generation).
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
    ///     Renders a specific layer's paths to a white mask image.
    /// </summary>
    public SKImage? RenderLayerToMask(MaskLayer layer)
    {
        if (layer.Paths.Count == 0 || CanvasSize == Size.Empty)
            return null;

        // Create a temporary surface
        using var surface = SKSurface.Create(new SKImageInfo(CanvasSize.Width, CanvasSize.Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Draw paths in white directly
        // We pass White as overrideColor, which RenderPenPath uses for non-erase paths
        using var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Fill;

        foreach (var penPath in layer.Paths)
            RenderPenPathToCanvas(canvas, penPath, paint, SKColors.White);

        return surface.Snapshot();
    }

    /// <summary>
    ///     Renders a pen path to a canvas. Delegates to PaintCanvasViewModel's shared implementation.
    /// </summary>
    /// <param name="overrideColor">If provided, uses this color instead of the path's color.</param>
    private static void RenderPenPathToCanvas(
        SKCanvas canvas,
        PenPath penPath,
        SKPaint paint,
        SKColor? overrideColor = null
    )
    {
        PaintCanvasViewModel.RenderPenPath(canvas, penPath, paint, overrideColor);
    }

    /// <summary>
    ///     Gets the dialog for this view model.
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
            // Clear existing layers and selection
            SelectedLayer = null;
            foreach (var layer in Layers)
                CleanupLayer(layer);
            Layers.Clear();
            layerCounter = 0;

            foreach (var layerNode in layersArray)
                if (layerNode is JsonObject layerObj)
                {
                    var layer = new MaskLayer();
                    layer.LoadStateFromJsonObject(layerObj);

                    // Subscribe to layer property changes (same as AddLayer)
                    layer.PropertyChanged += Layer_PropertyChanged;

                    Layers.Add(layer);
                    layerCounter++;
                }

            // Select first layer
            if (Layers.Count > 0)
                SelectedLayer = Layers[0];
        }

        // Ensure at least one layer exists
        if (Layers.Count == 0)
            AddLayer();

        // Always sync to canvas after loading to ensure paths are displayed
        SyncSelectedLayerToCanvas();
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
            layersArray.Add(layer.SaveStateToJsonObject());
        state["layers"] = layersArray;

        return state;
    }
}
