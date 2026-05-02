using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using AvaloniaColor = Avalonia.Media.Color;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Represents a single region in regional prompting.
/// Each region has a color (for painting), a prompt, and a strength.
/// </summary>
public partial class RegionEntry : ObservableObject
{
    /// <summary>
    /// Display name for the region (e.g., "Region 1").
    /// </summary>
    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>
    /// Color used to paint this region on the mask canvas.
    /// Stored internally but serialized via ColorHexValue.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private SKColor color = SKColors.Red;

    /// <summary>
    /// The prompt text for this region.
    /// </summary>
    [ObservableProperty]
    private string prompt = string.Empty;

    /// <summary>
    /// Strength of the conditioning for this region (0.0 - 10.0).
    /// </summary>
    [ObservableProperty]
    private double strength = 1.0;

    /// <summary>
    /// Whether this region is enabled.
    /// </summary>
    [ObservableProperty]
    private bool isEnabled = true;

    /// <summary>
    /// Gets or sets the color as a hex string for JSON serialization.
    /// </summary>
    [JsonPropertyName("color")]
    public string ColorHexValue
    {
        get => $"#{Color.Red:X2}{Color.Green:X2}{Color.Blue:X2}";
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
                Color = new SKColor(r, g, b);
            }
        }
    }

    /// <summary>
    /// Gets the color as a hex string (e.g., "#FF0000") for UI binding.
    /// </summary>
    [JsonIgnore]
    public string ColorHex => ColorHexValue;

    /// <summary>
    /// Gets the color as an Avalonia Color for UI binding.
    /// </summary>
    [JsonIgnore]
    public AvaloniaColor AvaloniaColorValue => AvaloniaColor.FromRgb(Color.Red, Color.Green, Color.Blue);
}

/// <summary>
/// Default color palette for regional prompting.
/// </summary>
public static class RegionalPromptColors
{
    public static readonly SKColor Red = new(255, 0, 0);
    public static readonly SKColor Orange = new(255, 128, 0);
    public static readonly SKColor Yellow = new(255, 255, 0);
    public static readonly SKColor Green = new(0, 255, 0);
    public static readonly SKColor Blue = new(0, 128, 255);
    public static readonly SKColor Purple = new(128, 0, 255);

    public static readonly IReadOnlyList<SKColor> DefaultPalette = [Red, Orange, Yellow, Green, Blue, Purple];
}
