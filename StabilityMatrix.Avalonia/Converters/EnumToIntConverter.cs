using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Data.Converters;
using KGySoft.CoreLibraries;

namespace StabilityMatrix.Avalonia.Converters;

public class EnumToIntConverter<TEnum> : IValueConverter
    where TEnum : struct, Enum
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TEnum enumValue)
        {
            return System.Convert.ToInt32(enumValue);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return Unsafe.As<int, TEnum>(ref intValue);
        }

        return null;
    }
}
