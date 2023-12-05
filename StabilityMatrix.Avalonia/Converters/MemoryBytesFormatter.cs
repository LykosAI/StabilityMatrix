using System;
using Size = StabilityMatrix.Core.Helper.Size;

namespace StabilityMatrix.Avalonia.Converters;

public class MemoryBytesFormatter : ICustomFormatter, IFormatProvider
{
    /// <inheritdoc />
    public object? GetFormat(Type? formatType)
    {
        return formatType == typeof(ICustomFormatter) ? this : null;
    }

    /// <inheritdoc />
    public string Format(string? format, object? arg, IFormatProvider? formatProvider)
    {
        if (format == null || !format.Trim().StartsWith('M'))
        {
            if (arg is IFormattable formatArg)
            {
                return formatArg.ToString(format, formatProvider);
            }

            return arg?.ToString() ?? string.Empty;
        }

        var value = Convert.ToUInt64(arg);

        var result = format.Trim().EndsWith("10", StringComparison.OrdinalIgnoreCase)
            ? Size.FormatBase10Bytes(value)
            : Size.FormatBytes(value);

        // Strip i if not Mi
        if (!format.Trim().Contains('I', StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace("i", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
