using System;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Converts a possibly boxed nullable value type to its default value
/// </summary>
public class NullableDefaultNumericConverter<TSource, TTarget> : IValueConverter
    where TSource : unmanaged, INumber<TSource>
    where TTarget : unmanaged, INumber<TTarget>
{
    public ReturnBehavior NanHandling { get; set; } = ReturnBehavior.DefaultValue;

    /// <summary>
    /// Unboxes a nullable value type
    /// </summary>
    private TSource Unbox(TTarget? value)
    {
        if (!value.HasValue)
        {
            return default;
        }

        if (TTarget.IsNaN(value.Value))
        {
            return NanHandling switch
            {
                ReturnBehavior.DefaultValue => default,
                ReturnBehavior.Throw
                    => throw new InvalidCastException("Cannot convert NaN to a numeric type"),
                _
                    => throw new InvalidEnumArgumentException(
                        nameof(NanHandling),
                        (int)NanHandling,
                        typeof(ReturnBehavior)
                    )
            };
        }

        return (TSource)System.Convert.ChangeType(value.Value, typeof(TSource));
    }

    /// <summary>
    /// Convert a value type to a nullable value type
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType != typeof(TTarget?) && !targetType.IsAssignableTo(typeof(TTarget)))
        {
            // ReSharper disable once LocalizableElement
            throw new ArgumentException(
                $"Convert Target type {targetType.Name} must be assignable to {typeof(TTarget).Name}"
            );
        }

        return (TTarget?)System.Convert.ChangeType(value, typeof(TTarget));
    }

    /// <summary>
    /// Convert a nullable value type to a value type
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsAssignableTo(typeof(TSource)))
        {
            // ReSharper disable once LocalizableElement
            throw new ArgumentException(
                $"ConvertBack Target type {targetType.Name} must be assignable to {typeof(TSource).Name}"
            );
        }

        return Unbox((TTarget?)value);
    }

    public enum ReturnBehavior
    {
        DefaultValue,
        Throw
    }
}
