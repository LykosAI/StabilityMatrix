using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;

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

    /// <summary>
    /// Get all values of type <typeparamref name="T"/> as a dictionary.
    /// Includes all public static properties.
    /// </summary>
    protected static Dictionary<string, T> GetValues<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >()
        where T : StringValue
    {
        var values = new Dictionary<string, T>();

        foreach (var field in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is T value)
            {
                // Exclude if IgnoreDataMember
                if (field.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null)
                    continue;

                values.Add(value.Value, value);
            }
        }

        return values;
    }
}
