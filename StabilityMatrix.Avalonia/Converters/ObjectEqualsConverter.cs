using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StabilityMatrix.Avalonia.Converters;

public class ObjectEqualsConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        var first = values[0];
        var second = values[1];

        if (first is null || second is null)
            return false;

        return ReferenceEquals(first, second) || first.Equals(second);
    }
}
