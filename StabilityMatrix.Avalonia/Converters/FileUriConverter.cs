using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public class FileUriConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType != typeof(Uri))
        {
            return null;
        }

        var str = value switch
        {
            string s => s,
            IFormattable formattable => formattable.ToString(null, culture),
            _ => null,
        };

        return str switch
        {
            null or "" => null,
            _ when str.StartsWith("avares://") => new Uri(str),
            _ when str.StartsWith("https://") || str.StartsWith("http://") => new Uri(str),
            // Raw absolute file path: Uri's file-path parsing escapes reserved characters
            // ("#", "%", spaces). Prepending "file://" instead would parse "#" as a fragment
            // delimiter and truncate the path.
            _ when Path.IsPathRooted(str) => new Uri(str),
            _ => null,
        };
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType == typeof(string) && value is Uri uri)
        {
            return uri.IsFile ? uri.LocalPath : uri.ToString();
        }

        return null;
    }
}
