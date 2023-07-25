using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public class FitSquarelyWithinAspectRatioConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var bounds = value is Rect rect ? rect : default;
        return Math.Min(bounds.Width, bounds.Height);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
