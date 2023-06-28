using System;
using System.Globalization;
using System.Windows.Data;

namespace StabilityMatrix.Converters;

[ValueConversion(typeof(string), typeof(bool))]
public class IsStringNullOrWhitespaceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue)
        {
            return string.IsNullOrWhiteSpace(strValue);
        }

        throw new InvalidOperationException("Cannot convert non-string value");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
