using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts a boolean to a <see cref="GridLength"/>. The true/false results are supplied via
/// the <c>ConverterParameter</c> in the form <c>"trueValue|falseValue"</c> — values can be
/// pixel sizes (<c>"100"</c>), star sizes (<c>"3*"</c>), or <c>"Auto"</c>. Defaults to
/// <c>"*|0"</c> when no valid parameter is provided. The pipe separator (not comma) sidesteps
/// XAML markup-extension argument-splitting quirks.
/// </summary>
public class BoolToGridLengthConverter : IValueConverter
{
    public static readonly BoolToGridLengthConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (trueValue, falseValue) = ParseParameter(parameter);
        return value is true ? trueValue : falseValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

    private static (GridLength TrueValue, GridLength FalseValue) ParseParameter(object? parameter)
    {
        if (parameter is string s && s.Split('|') is { Length: 2 } parts)
        {
            return (Parse(parts[0]), Parse(parts[1]));
        }
        return (new GridLength(1, GridUnitType.Star), new GridLength(0));
    }

    private static GridLength Parse(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return GridLength.Auto;

        if (trimmed.EndsWith('*'))
        {
            var coefStr = trimmed[..^1];
            if (string.IsNullOrEmpty(coefStr))
                return new GridLength(1, GridUnitType.Star);
            if (double.TryParse(coefStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var coef))
                return new GridLength(coef, GridUnitType.Star);
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
            return new GridLength(px);

        return GridLength.Auto;
    }
}
