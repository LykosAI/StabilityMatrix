using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public class BooleanChoiceMultiConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (values.Count < 3)
        {
            return null;
        }

        if (values[0] is bool boolValue)
        {
            return boolValue ? values[1] : values[2];
        }

        return null;
    }
}
