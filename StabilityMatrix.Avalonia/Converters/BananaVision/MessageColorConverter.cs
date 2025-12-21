using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace StabilityMatrix.Avalonia.Converters.BananaVision;

public class MessageColorConverter : IValueConverter
{
    // iOS Blue
    private readonly SolidColorBrush myColor = new(Color.Parse("#007AFF"));

    // iOS Light Grey
    private readonly SolidColorBrush theirColor = new(Color.Parse("#E5E5EA"));

    // iOS Dark Mode Blue
    private readonly SolidColorBrush myColorDark = new(Color.Parse("#0A84FF"));

    // iOS Dark Mode Grey (Secondary System Fill)
    private readonly SolidColorBrush theirColorDark = new(Color.Parse("#262626"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
        {
            return value is true ? myColorDark : theirColorDark;
        }

        return value is true ? myColor : theirColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
