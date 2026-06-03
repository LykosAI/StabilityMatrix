using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters.BananaVision;

/// <summary>
/// Converts provider IDs to user-friendly display names
/// </summary>
public class ProviderIdToDisplayNameConverter : IValueConverter
{
    public static readonly ProviderIdToDisplayNameConverter Instance = new();

    private static readonly Dictionary<string, string> ProviderDisplayNames = new()
    {
        ["gemini-2.5-flash"] = "Gemini 2.5 Flash",
        ["gemini-3.1-flash"] = "Gemini 3.1 Flash",
        ["gemini-3-pro"] = "Gemini 3 Pro",
        ["flux-kontext"] = "Flux Kontext",
        ["qwen-image-edit"] = "Qwen Image Edit",
        ["flux2-klein"] = "Flux.2 Klein",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string providerId && ProviderDisplayNames.TryGetValue(providerId, out var displayName))
        {
            return displayName;
        }

        // Return the original ID if no mapping found
        return value?.ToString() ?? "Unknown";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
