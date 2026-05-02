using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public class FileExistsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
