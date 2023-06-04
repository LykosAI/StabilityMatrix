using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace StabilityMatrix.Converters;

public class UriToBitmapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Uri uri)
        {
            return new BitmapImage(uri);
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
