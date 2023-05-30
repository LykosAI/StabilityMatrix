using System;
using System.Globalization;
using System.Windows.Data;

namespace StabilityMatrix.Converters;

public class IntDoubleConverter : IValueConverter
{
    // Convert from int to double
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType == typeof(double?))
        {
            return System.Convert.ToDouble(value);
        }

        throw new ArgumentException($"Unsupported type {targetType}");
    }

    // Convert from double to int (floor)
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType == typeof(int?))
        {
            return System.Convert.ToInt64(value);
        }

        throw new ArgumentException($"Unsupported type {targetType}");
    }
}
