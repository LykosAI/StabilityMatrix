using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts an enum value to an attribute
/// </summary>
/// <typeparam name="TAttribute">Type of attribute</typeparam>
public class EnumAttributeConverter<TAttribute>(Func<TAttribute, object?>? accessor = null) : IValueConverter
    where TAttribute : Attribute
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return null;

        if (value is not Enum @enum)
            throw new ArgumentException("Value must be an enum");

        var field = @enum.GetType().GetField(@enum.ToString());
        if (field is null)
            throw new ArgumentException("Value must be an enum");

        if (field.GetCustomAttributes<TAttribute>().FirstOrDefault() is not { } attribute)
            throw new ArgumentException($"Enum value {@enum} does not have attribute {typeof(TAttribute)}");

        return accessor is not null ? accessor(attribute) : attribute;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
