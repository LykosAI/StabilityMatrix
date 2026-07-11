using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace StabilityMatrix.Avalonia.Converters.BananaVision;

public class MessageTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // always white for dark mode
        if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
            return Brushes.White;

        // White text for blue bubbles, Black text for grey bubbles
        return value is true ? Brushes.White : Brushes.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
