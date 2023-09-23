using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public static class StringFormatConverters
{
    private static StringFormatValueConverter? _decimalConverter;
    public static StringFormatValueConverter Decimal =>
        _decimalConverter ??= new StringFormatValueConverter("{0:D}", null);
}
