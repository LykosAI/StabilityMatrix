using System;
using System.Collections.Generic;
using System.Linq;
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

    static BetterPropertyGrid()
    {
        // Initialize localization and name resolver
        LocalizationService.Default.AddExtraService(new PropertyGridLocalizationService());
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

        // Uncheck All and check Misc by default
        categoryFilter.UnCheck(categoryFilter.All);
        categoryFilter.Check("Misc");

        // Check included categories
        foreach (var mask in includeCategories)
        {
            categoryFilter.Check(mask);
        }

        categoryFilter.EndUpdate();
    }
}
