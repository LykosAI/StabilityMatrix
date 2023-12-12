using System;
using System.Globalization;
using Avalonia.Data.Converters;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Converters;

public class EnumStringConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Enum enumValue)
            return null;

        return enumValue.GetStringValue();
    }

    /// <inheritdoc />
    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (value is not string stringValue)
            return null;

        return Enum.Parse(targetType, stringValue);
    }
}
