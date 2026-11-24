using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Converters;

/// <summary>
/// Category names → coloured <see cref="CategoryChip"/>s, resolving colours from the settings
/// registry itself. For shared templates (e.g. the model combobox items) whose data context is a
/// plain model and which have no view model to hand the palette through; prefer
/// <see cref="CategoryChipsConverter"/> where a palette can be bound.
/// </summary>
public class CategoryChipsFromSettingsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IReadOnlyList<string> { Count: > 0 } categories)
        {
            return Array.Empty<CategoryChip>();
        }

        var registry = App.Services?.GetService<ISettingsManager>()?.Settings.ModelCategories
            ?? (IReadOnlyList<ModelCategory>)[];

        return ModelCategoryStyle.ChipsFor(categories, registry);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
