using System;
using System.Globalization;
using System.Windows.Data;

namespace StabilityMatrix.Converters;

public class LaunchOptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (targetType == typeof(string))
        {
            return value?.ToString() ?? "";
        }

        if (targetType == typeof(bool?))
        {
            return bool.TryParse(value?.ToString(), out var boolValue) && boolValue;
        }

        if (targetType == typeof(double?))
        {
            if (value == null)
            {
                return null;
            }
            return double.TryParse(value?.ToString(), out var doubleValue) ? doubleValue : 0;
        }
        
        if (targetType == typeof(int?))
        {
            if (value == null)
            {
                return null;
            }
            return int.TryParse(value?.ToString(), out var intValue) ? intValue : 0;
        }

        throw new ArgumentException("Unsupported type");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
