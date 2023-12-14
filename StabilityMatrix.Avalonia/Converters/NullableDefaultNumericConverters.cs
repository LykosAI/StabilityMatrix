namespace StabilityMatrix.Avalonia.Converters;

public static class NullableDefaultNumericConverters
{
    public static readonly NullableDefaultNumericConverter<int, decimal> IntToDecimal = new();
}
