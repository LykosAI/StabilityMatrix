using System;
using System.Globalization;
using Avalonia.Data.Converters;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts a <see cref="NumberFormatMode"/> to a sample number string
/// </summary>
public class NumberFormatModeSampleConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not NumberFormatMode mode)
            return null;

        const double sample = 12345.67;

        // Format the sample number based on the number format mode
        return mode switch
        {
            NumberFormatMode.Default => sample.ToString("N2", culture),
            NumberFormatMode.CurrentCulture => sample.ToString("N2", culture),
            NumberFormatMode.InvariantCulture => sample.ToString("N2", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
