using System;
using System.Globalization;
using Avalonia.Data.Converters;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts an enum value to its <c>[Description]</c> text, falling back to the enum name.
/// </summary>
public class EnumDescriptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
        {
            return enumValue.GetDescription() ?? enumValue.ToString();
        }

        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
