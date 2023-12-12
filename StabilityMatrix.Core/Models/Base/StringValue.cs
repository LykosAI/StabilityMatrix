namespace StabilityMatrix.Core.Models.Base;

/// <summary>
/// Base class for a string value object
/// </summary>
/// <param name="Value">String value</param>
public abstract record StringValue(string Value)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }

    public static implicit operator string(StringValue stringValue) => stringValue.ToString();
}
