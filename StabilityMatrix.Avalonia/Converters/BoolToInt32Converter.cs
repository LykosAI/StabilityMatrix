using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public static class BoolToInt32Converter
{
    public static readonly IValueConverter OneIfTrueElseZero = new FuncValueConverter<bool, int>(b =>
        b ? 1 : 0
    );

    public static readonly IValueConverter OneIfTrueElseTwo = new FuncValueConverter<bool, int>(b =>
        b ? 1 : 2
    );
}
