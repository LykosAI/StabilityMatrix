using System;

namespace StabilityMatrix.Avalonia.Converters;

public class KiloFormatter : ICustomFormatter, IFormatProvider
{
    public object? GetFormat(Type? formatType)
    {
        return formatType == typeof(ICustomFormatter) ? this : null;
    }

    public string Format(string? format, object? arg, IFormatProvider? formatProvider)
    {
        if (format == null || !format.Trim().StartsWith('K'))
        {
            if (arg is IFormattable formatArg)
            {
                return formatArg.ToString(format, formatProvider);
            }

            return arg?.ToString() ?? string.Empty;
        }

        var value = Convert.ToInt64(arg);

        return FormatNumber(value);
    }

    private static string FormatNumber(long num)
    {
        if (num >= 100000000)
        {
            return (num / 1000000D).ToString("0.#M");
        }
        if (num >= 1000000)
        {
            return (num / 1000000D).ToString("0.##M");
        }
        if (num >= 100000)
        {
            return (num / 1000D).ToString("0.#K");
        }
        if (num >= 10000)
        {
            return (num / 1000D).ToString("0.##K");
        }

        return num.ToString("#,0");
    }
}
