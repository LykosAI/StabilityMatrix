using System;
using System.Globalization;
using System.Windows.Data;

namespace StabilityMatrix.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool?)
        {
            var boolVal = value as bool?;
            return !boolVal ?? false;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}
