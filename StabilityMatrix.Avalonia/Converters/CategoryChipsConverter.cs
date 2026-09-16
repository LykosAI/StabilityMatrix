using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Multi-value converter: [category names, category registry] → coloured <see cref="CategoryChip"/>s.
/// Lets templates that bind plain model data render chips without a service lookup.
/// </summary>
public class CategoryChipsConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] is not IReadOnlyList<string> { Count: > 0 } categories)
        {
            return Array.Empty<CategoryChip>();
        }

        var registry = values.Count > 1 && values[1] is IReadOnlyList<ModelCategory> list ? list : [];

        return ModelCategoryStyle.ChipsFor(categories, registry);
    }
}
