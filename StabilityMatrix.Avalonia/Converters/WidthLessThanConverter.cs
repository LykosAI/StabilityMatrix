using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts a numeric width (typically <c>Bounds.Width</c>) to a bool indicating whether
/// the width is strictly less than a threshold passed via <c>ConverterParameter</c>.
/// Used for pure-XAML responsive layouts: bind <c>Classes.compact</c> to this and a Grid /
/// Panel can self-restructure when its container gets narrower than the threshold.
///
/// Example:
/// <code>
/// &lt;Grid Classes.compact="{Binding $self.Bounds.Width,
///         Converter={x:Static converters:WidthLessThanConverter.Instance},
///         ConverterParameter=720}" /&gt;
/// </code>
/// </summary>
public class WidthLessThanConverter : IValueConverter
{
    public static readonly WidthLessThanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double width || double.IsNaN(width) || width <= 0)
        {
            // Width not measured yet — don't claim "compact" prematurely, since the binding
            // fires once with zero before layout settles.
            return false;
        }

        var threshold = ParseThreshold(parameter);
        return width < threshold;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

    private static double ParseThreshold(object? parameter)
    {
        return parameter switch
        {
            double d => d,
            int i => i,
            string s
                when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) =>
                parsed,
            _ => 720d,
        };
    }
}
