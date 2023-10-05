using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts an Uri to a string, excluding leading protocol and trailing slashes
/// </summary>
public class UriStringConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Uri uri)
        {
            return (
                uri.Host
                + (uri.IsDefaultPort ? "" : $":{uri.Port}")
                + uri.PathAndQuery
                + uri.Fragment
            ).TrimEnd('/');
        }

        return null;
    }

    /// <inheritdoc />
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        throw new NotImplementedException();
    }
}
