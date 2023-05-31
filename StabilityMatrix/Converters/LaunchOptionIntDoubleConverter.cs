using System;
using System.Globalization;
using System.Windows.Data;

namespace StabilityMatrix.Converters;

public class LaunchOptionIntDoubleConverter : IValueConverter
{
    // Convert from int to double
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {   
        if (targetType == typeof(double?))
        {
            if (value == null)
            {
                return null;
            }
            return System.Convert.ToDouble(value);
        }

        throw new ArgumentException($"Unsupported type {targetType}");
    }

    // Convert from double to object int (floor)
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType == typeof(int?) || targetType == typeof(object))
        {
            return System.Convert.ToInt32(value);
        }

        throw new ArgumentException($"Unsupported type {targetType}");
    }
}
