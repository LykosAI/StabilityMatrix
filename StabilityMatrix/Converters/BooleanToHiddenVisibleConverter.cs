using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StabilityMatrix.Converters;

public class BooleanToHiddenVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var bValue = false;
        if (value is bool b)
        {
            bValue = b;
        }
        else if (value is bool)
        {
            var tmp = (bool?) value;
            bValue = tmp.Value;
        }
        return bValue ? Visibility.Visible : Visibility.Hidden;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}
