namespace StabilityMatrix.Core.Models;

public abstract record StringValue(string Value) : IFormattable
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return Value;
    }
}
