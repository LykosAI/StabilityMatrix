using System;

namespace StabilityMatrix.Extensions;

public static class EnumConversionExtensions
{
    public static T? ConvertTo<T>(this Enum value) where T : Enum
    {
        var type = value.GetType();
        var fieldInfo = type.GetField(value.ToString());
        // Get the string value attributes
        var attribs = fieldInfo?.GetCustomAttributes(typeof(ConvertToAttribute<T>), false) as ConvertToAttribute<T>[];
        // Return the first if there was a match.
        return attribs?.Length > 0 ? attribs[0].ConvertToEnum : default;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class ConvertToAttribute<T> : Attribute where T : Enum
{
    public T ConvertToEnum { get; }
    public ConvertToAttribute(T toEnum)
    {
        ConvertToEnum = toEnum;
    }
}
