using System;
using System.Windows;
using System.Windows.Data;

namespace StabilityMatrix.Converters;

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }
    public object? ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return null;
    }
}
