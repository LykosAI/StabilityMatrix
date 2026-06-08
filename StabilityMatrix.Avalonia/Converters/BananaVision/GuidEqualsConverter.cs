using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters.BananaVision;

/// <summary>
/// Multi-value converter that returns true if two Guid values are equal.
/// Used to show generating indicator on conversation list items.
/// </summary>
public class GuidEqualsConverter : IMultiValueConverter
{
    public static readonly GuidEqualsConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        var first = values[0] as Guid?;
        var second = values[1] as Guid?;

        // If either is null, they're not equal (unless both are null, but that's unlikely here)
        if (first == null || second == null)
            return false;

        return first.Value == second.Value;
    }
}
