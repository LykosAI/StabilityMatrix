using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.PropertyGrid.Services;
using JetBrains.Annotations;
using PropertyModels.ComponentModel;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Controls;

/// <inheritdoc />
[PublicAPI]
public class BetterPropertyGrid : global::Avalonia.PropertyGrid.Controls.PropertyGrid
{
    protected override Type StyleKeyOverride => typeof(global::Avalonia.PropertyGrid.Controls.PropertyGrid);

    public static readonly StyledProperty<IEnumerable<string>> ExcludedCategoriesProperty = AvaloniaProperty.Register<
        BetterPropertyGrid,
        IEnumerable<string>
    >("ExcludedCategories");

    public IEnumerable<string> ExcludedCategories
    {
        get => GetValue(ExcludedCategoriesProperty);
        set => SetValue(ExcludedCategoriesProperty, value);
    }

    public static readonly StyledProperty<IEnumerable<string>> IncludedCategoriesProperty = AvaloniaProperty.Register<
        BetterPropertyGrid,
        IEnumerable<string>
    >("IncludedCategories");

    public IEnumerable<string> IncludedCategories
    {
        get => GetValue(IncludedCategoriesProperty);
        set => SetValue(IncludedCategoriesProperty, value);
    }

    static BetterPropertyGrid()
    {
        // Initialize localization and name resolver
        LocalizationService.Default.AddExtraService(new PropertyGridLocalizationService());

        ExcludedCategoriesProperty
            .Changed
            .AddClassHandler<BetterPropertyGrid>(
                (grid, args) =>
                {
                    if (args.NewValue is IEnumerable<string> excludedCategories)
                    {
                        grid.FilterExcludeCategories(excludedCategories);
                    }
                }
            );

        IncludedCategoriesProperty
            .Changed
            .AddClassHandler<BetterPropertyGrid>(
                (grid, args) =>
                {
                    if (args.NewValue is IEnumerable<string> includedCategories)
                    {
                        grid.FilterIncludeCategories(includedCategories);
                    }
                }
            );
    }

    public void FilterExcludeCategories(IEnumerable<string> excludedCategories)
    {
        // Get internal property `ViewModel` of internal type `PropertyGridViewModel`
        var gridVm = this.GetProtectedProperty("ViewModel")!;
        // Get public property `CategoryFilter`
        var categoryFilter = gridVm.GetProtectedProperty<CheckedMaskModel>("CategoryFilter")!;

        categoryFilter.BeginUpdate();

        // Uncheck All, then check all except All
        categoryFilter.UnCheck(categoryFilter.All);

        foreach (var mask in categoryFilter.Masks.Where(m => m != categoryFilter.All))
        {
            categoryFilter.Check(mask);
        }

        // Uncheck excluded categories
        foreach (var mask in excludedCategories)
        {
            categoryFilter.UnCheck(mask);
        }

        categoryFilter.EndUpdate();
    }

    public void FilterIncludeCategories(IEnumerable<string> includeCategories)
    {
        // Get internal property `ViewModel` of internal type `PropertyGridViewModel`
        var gridVm = this.GetProtectedProperty("ViewModel")!;
        // Get public property `CategoryFilter`
        var categoryFilter = gridVm.GetProtectedProperty<CheckedMaskModel>("CategoryFilter")!;

        categoryFilter.BeginUpdate();

        // Uncheck non-included categories
        foreach (var mask in categoryFilter.Masks.Where(m => !includeCategories.Contains(m)))
        {
            categoryFilter.UnCheck(mask);
        }

        categoryFilter.UnCheck(categoryFilter.All);

        // Check included categories
        foreach (var mask in includeCategories)
        {
            categoryFilter.Check(mask);
        }

        categoryFilter.EndUpdate();
    }
}
