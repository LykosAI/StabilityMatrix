using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SysDrawColor = System.Drawing.Color;

namespace StabilityMatrix.Avalonia.Diagnostics.LogViewer.Converters;

public class ChangeColorTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return new SolidColorBrush((Color)(parameter ?? Colors.Black));

        var sysDrawColor = (SysDrawColor)value!;
        return new SolidColorBrush(
            Color.FromArgb(sysDrawColor.A, sysDrawColor.R, sysDrawColor.G, sysDrawColor.B)
        );
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotImplementedException();
}
