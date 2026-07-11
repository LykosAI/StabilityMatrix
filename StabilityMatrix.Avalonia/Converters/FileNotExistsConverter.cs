using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public class FileNotExistsConverter : IValueConverter
{
    private static readonly FileExistsConverter ExistsConverter = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var exists = ExistsConverter.Convert(value, targetType, parameter, culture);
        return exists is false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
