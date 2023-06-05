using System;
using System.Linq;
using System.Windows.Ink;

namespace StabilityMatrix.Extensions;

public static class EnumAttributeExtensions
{
    private static T? GetAttributeValue<T>(Enum value)
    {
        var type = value.GetType();
        var fieldInfo = type.GetField(value.ToString());
        // Get the string value attributes
        var attribs = fieldInfo?.GetCustomAttributes(typeof(T), false) as T[];
        // Return the first if there was a match.
        return attribs?.Length > 0 ? attribs[0] : default;
    }
    /// <summary>
    /// Gets the StringValue field attribute on a given enum value.
    /// If not found, returns the enum value itself as a string.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string GetStringValue(this Enum value)
    {
        var attr = GetAttributeValue<StringValueAttribute>(value)?.StringValue;
        return attr ?? Enum.GetName(value.GetType(), value)!;
    }
    /// <summary>
    /// Gets the Description field attribute on a given enum value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string? GetDescription(this Enum value)
    {
        return GetAttributeValue<DescriptionAttribute>(value)?.Description;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class StringValueAttribute : Attribute
{
    public string StringValue { get; }
    public StringValueAttribute(string value) {
        StringValue = value;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class DescriptionAttribute : Attribute
{
    public string Description { get; }
    public DescriptionAttribute(string value) {
        Description = value;
    }
}
