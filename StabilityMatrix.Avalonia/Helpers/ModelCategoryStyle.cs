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

    /// <summary>
    /// Preset chip colours offered in the colour picker, ordered so neighbours contrast.
    /// New categories take the first one no registered category uses yet.
    /// </summary>
    public static IReadOnlyList<Color> Palette { get; } =
        new[]
        {
            "#3B82F6",
            "#F59E0B",
            "#10B981",
            "#F43F5E",
            "#8B5CF6",
            "#06B6D4",
            "#F97316",
            "#84CC16",
            "#EC4899",
            "#14B8A6",
            "#6366F1",
            "#EF4444",
            "#EAB308",
            "#22C55E",
            "#D946EF",
            "#64748B",
        }
            .Select(Color.Parse)
            .ToList();

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
    /// The first <see cref="Palette"/> colour none of the given categories uses, cycling through
    /// the palette once every colour is taken. Categories without a colour count as
    /// <see cref="DefaultColor"/>.
    /// </summary>
    public static Color NextPaletteColor(IReadOnlyList<ModelCategory> existing)
    {
        var used = existing.Select(c => ParseColor(c.Color)).ToHashSet();
        return Palette.FirstOrDefault(
            color => !used.Contains(color),
            Palette[existing.Count % Palette.Count]
        );
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
