using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls.Models;
using StabilityMatrix.Avalonia.Models;
using AvaloniaColor = Avalonia.Media.Color;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Default color palette for layer visual distinction in the editor.
/// </summary>
public static class MaskLayerColors
{
    public static readonly SKColor Red = new(255, 100, 100);
    public static readonly SKColor Orange = new(255, 180, 100);
    public static readonly SKColor Yellow = new(255, 255, 100);
    public static readonly SKColor Green = new(100, 255, 150);
    public static readonly SKColor Blue = new(100, 180, 255);
    public static readonly SKColor Purple = new(180, 100, 255);
    public static readonly SKColor Pink = new(255, 100, 200);
    public static readonly SKColor Cyan = new(100, 255, 255);

    public static readonly IReadOnlyList<SKColor> DefaultPalette =
    [
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple,
        Pink,
        Cyan,
    ];

    /// <summary>
    /// Gets a color from the palette by index (wraps around).
    /// </summary>
    public static SKColor GetByIndex(int index) => DefaultPalette[index % DefaultPalette.Count];
}

/// <summary>
/// Type of mask layer - determines how the layer content is rendered and edited.
/// </summary>
public enum MaskLayerType
{
    /// <summary>
    /// A painted mask layer with brush strokes. This is the default type.
    /// </summary>
    Paint,

    /// <summary>
    /// An image layer that displays a bitmap. Future feature for painting over images.
    /// </summary>
    Image,
}

/// <summary>
/// Represents a single layer in the layered mask editor.
/// Each layer has its own painted mask, prompt, and compositing settings.
/// </summary>
public partial class MaskLayer : ObservableObject, IJsonLoadableState
{
    /// <summary>
    /// Display name for the layer (e.g., "Layer 1").
    /// </summary>
    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>
    /// The type of this layer (Paint or Image).
    /// </summary>
    [ObservableProperty]
    private MaskLayerType layerType = MaskLayerType.Paint;

    /// <summary>
    /// Path to the source image for Image layers. Null for Paint layers.
    /// </summary>
    [ObservableProperty]
    private string? sourceImagePath;

    /// <summary>
    /// The loaded source image bitmap for Image layers. Runtime only, not serialized.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private SKBitmap? sourceImage;

    /// <summary>
    /// Scale factor for image layers (0.1 to 3.0, default 1.0).
    /// Allows user to resize the reference image.
    /// </summary>
    [ObservableProperty]
    private double imageScale = 1.0;

    /// <summary>
    /// The prompt text for this layer's region.
    /// </summary>
    [ObservableProperty]
    private string prompt = string.Empty;

    /// <summary>
    /// Conditioning strength for this region (0.0 - 10.0, default 1.0).
    /// </summary>
    [ObservableProperty]
    private double strength = 1.0;

    /// <summary>
    /// Compositing opacity for editor preview (0.0 - 1.0, default 1.0).
    /// This affects how the layer is displayed in the editor, not the final mask.
    /// </summary>
    [ObservableProperty]
    private double opacity = 1.0;

    /// <summary>
    /// Whether this layer is visible in the editor preview.
    /// </summary>
    [ObservableProperty]
    private bool isVisible = true;

    /// <summary>
    /// Whether this layer is enabled for generation.
    /// Disabled layers are skipped during mask generation.
    /// </summary>
    [ObservableProperty]
    private bool isEnabled = true;

    /// <summary>
    /// Display color for this layer in the editor (for visual distinction).
    /// Stored internally but serialized via ColorHex.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private SKColor displayColor = MaskLayerColors.Red;

    /// <summary>
    /// The painted stroke paths for this layer.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private ImmutableList<PenPath> paths = [];

    /// <summary>
    /// Gets or sets the display color as hex for JSON serialization.
    /// </summary>
    [JsonPropertyName("displayColor")]
    public string DisplayColorHex
    {
        get => $"#{DisplayColor.Red:X2}{DisplayColor.Green:X2}{DisplayColor.Blue:X2}";
        set
        {
            if (string.IsNullOrEmpty(value))
                return;

            var hex = value.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex[..2], 16);
                var g = Convert.ToByte(hex[2..4], 16);
                var b = Convert.ToByte(hex[4..6], 16);
                DisplayColor = new SKColor(r, g, b);
            }
        }
    }

    /// <summary>
    /// Gets the display color as an Avalonia Color for UI binding.
    /// </summary>
    [JsonIgnore]
    public AvaloniaColor AvaloniaDisplayColor =>
        AvaloniaColor.FromRgb(DisplayColor.Red, DisplayColor.Green, DisplayColor.Blue);

    /// <summary>
    /// Whether this layer has any content (paint strokes or image).
    /// Empty layers are skipped during generation.
    /// </summary>
    [JsonIgnore]
    public bool HasContent => LayerType == MaskLayerType.Paint ? Paths.Count > 0 : HasImage;

    /// <summary>
    /// Whether this image layer has a loaded source image.
    /// </summary>
    [JsonIgnore]
    public bool HasImage => SourceImage != null || !string.IsNullOrEmpty(SourceImagePath);

    /// <summary>
    /// Serialized paths for JSON persistence.
    /// </summary>
    [JsonPropertyName("paths")]
    public List<PenPath>? PathsForSerialization
    {
        get => Paths.Count > 0 ? Paths.ToList() : null;
        set => Paths = value?.ToImmutableList() ?? [];
    }

    /// <inheritdoc />
    public void LoadStateFromJsonObject(System.Text.Json.Nodes.JsonObject state, int version)
    {
        LoadStateFromJsonObject(state);
    }

    /// <inheritdoc />
    public void LoadStateFromJsonObject(System.Text.Json.Nodes.JsonObject state)
    {
        if (state.TryGetPropertyValue("name", out var nameNode))
            Name = nameNode?.GetValue<string>() ?? string.Empty;

        if (state.TryGetPropertyValue("layerType", out var layerTypeNode))
        {
            var layerTypeStr = layerTypeNode?.GetValue<string>() ?? "Paint";
            LayerType = Enum.TryParse<MaskLayerType>(layerTypeStr, out var parsedType)
                ? parsedType
                : MaskLayerType.Paint;
        }

        if (state.TryGetPropertyValue("sourceImagePath", out var imagePathNode))
            SourceImagePath = imagePathNode?.GetValue<string>();

        if (state.TryGetPropertyValue("imageScale", out var scaleNode))
            ImageScale = scaleNode?.GetValue<double>() ?? 1.0;

        if (state.TryGetPropertyValue("prompt", out var promptNode))
            Prompt = promptNode?.GetValue<string>() ?? string.Empty;

        if (state.TryGetPropertyValue("strength", out var strengthNode))
            Strength = strengthNode?.GetValue<double>() ?? 1.0;

        if (state.TryGetPropertyValue("opacity", out var opacityNode))
            Opacity = opacityNode?.GetValue<double>() ?? 1.0;

        if (state.TryGetPropertyValue("isVisible", out var visibleNode))
            IsVisible = visibleNode?.GetValue<bool>() ?? true;

        if (state.TryGetPropertyValue("isEnabled", out var enabledNode))
            IsEnabled = enabledNode?.GetValue<bool>() ?? true;

        if (state.TryGetPropertyValue("displayColor", out var colorNode))
            DisplayColorHex = colorNode?.GetValue<string>() ?? "#FF6464";

        if (
            state.TryGetPropertyValue("paths", out var pathsNode)
            && pathsNode is System.Text.Json.Nodes.JsonArray pathsArray
        )
        {
            var paths = pathsArray.Deserialize<List<PenPath>>() ?? [];
            Paths = paths.ToImmutableList();
        }
    }

    /// <inheritdoc />
    public System.Text.Json.Nodes.JsonObject SaveStateToJsonObject()
    {
        var state = new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = Name,
            ["layerType"] = LayerType.ToString(),
            ["prompt"] = Prompt,
            ["strength"] = Strength,
            ["opacity"] = Opacity,
            ["isVisible"] = IsVisible,
            ["isEnabled"] = IsEnabled,
            ["displayColor"] = DisplayColorHex,
        };

        // Save image layer properties
        if (LayerType == MaskLayerType.Image && !string.IsNullOrEmpty(SourceImagePath))
        {
            state["sourceImagePath"] = SourceImagePath;
            state["imageScale"] = ImageScale;
        }

        if (Paths.Count > 0)
        {
            state["paths"] = System.Text.Json.JsonSerializer.SerializeToNode(Paths.ToList());
        }

        return state;
    }
}
