using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// A category rendered as a card chip: its name plus the brushes derived from the category colour.
/// </summary>
public sealed record CategoryChip(string Name, IBrush Background, IBrush Foreground);

/// <summary>
/// Turns a category's stored colour into chip brushes. Chips sit on top of card artwork in both
/// themes, so like the other tag chips they use fixed fills rather than theme resources.
/// </summary>
public static class ModelCategoryStyle
{
    /// <summary>
    /// Used when a category has no colour set. Matches the default tag chip fill.
    /// </summary>
    public static readonly Color DefaultColor = Color.Parse("#3B82F6");

    private static readonly IBrush LightForeground = new SolidColorBrush(Color.Parse("#F5FAFF"));
    private static readonly IBrush DarkForeground = new SolidColorBrush(Color.Parse("#1B1B1F"));

    /// <summary>
    /// Parses a stored hex colour, falling back to <see cref="DefaultColor"/> when missing or invalid.
    /// </summary>
    public static Color ParseColor(string? hex)
    {
        return !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out var color) ? color : DefaultColor;
    }

    public static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>
    /// Solid brush for list dots and swatches.
    /// </summary>
    public static IBrush SwatchBrush(string? hex)
    {
        return new SolidColorBrush(ParseColor(hex));
    }

    public static CategoryChip ChipFor(string name, string? hex)
    {
        var color = ParseColor(hex);

        // Same 80% alpha as the base tag chip so category chips sit at the same weight
        var background = new SolidColorBrush(new Color(0xCC, color.R, color.G, color.B));

        return new CategoryChip(name, background, IsLight(color) ? DarkForeground : LightForeground);
    }

    /// <summary>
    /// Builds chips for a model's categories, looking colours up in the registry by name.
    /// </summary>
    public static IReadOnlyList<CategoryChip> ChipsFor(
        IReadOnlyList<string> categories,
        IReadOnlyList<ModelCategory> registry
    )
    {
        if (categories.Count == 0)
        {
            return [];
        }

        return categories
            .Select(name =>
            {
                var registered = registry.FirstOrDefault(c =>
                    string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase)
                );
                return ChipFor(registered?.Name ?? name, registered?.Color);
            })
            .ToList();
    }

    private static bool IsLight(Color color)
    {
        // Relative luminance (Rec. 709) on the 0–255 channels
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
        return luminance > 0.6;
    }
}
