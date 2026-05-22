using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts a boolean to an integer. The true/false results are supplied via the
/// <c>ConverterParameter</c> in the form <c>"trueValue,falseValue"</c> (e.g. <c>"1,0"</c>).
/// Falls back to <c>1</c>/<c>0</c> when no valid parameter is provided.
/// </summary>
public class BoolToIntConverter : IValueConverter
{
    public static readonly BoolToIntConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (trueValue, falseValue) = ParseParameter(parameter);
        return value is true ? trueValue : falseValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

    private static (int TrueValue, int FalseValue) ParseParameter(object? parameter)
    {
        if (
            parameter is string s
            && s.Split(',') is { Length: 2 } parts
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trueValue)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var falseValue)
        )
        {
            return (trueValue, falseValue);
        }

        return (1, 0);
    }
}
