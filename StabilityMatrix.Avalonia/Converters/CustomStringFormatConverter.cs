using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public class CustomStringFormatConverter<T>([StringSyntax("CompositeFormat")] string format)
    : IValueConverter
    where T : IFormatProvider, new()
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null ? null : string.Format(new T(), format, value);
    }

    /// <inheritdoc />
    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        return value is null ? null : throw new NotImplementedException();
    }
}
