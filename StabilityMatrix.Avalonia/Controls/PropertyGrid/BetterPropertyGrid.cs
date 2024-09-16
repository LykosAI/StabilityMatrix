using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    public static readonly StyledProperty<IEnumerable<string>> ExcludedCategoriesProperty =
        AvaloniaProperty.Register<BetterPropertyGrid, IEnumerable<string>>("ExcludedCategories");

    public IEnumerable<string> ExcludedCategories
    {
        get => GetValue(ExcludedCategoriesProperty);
        set => SetValue(ExcludedCategoriesProperty, value);
    }

    public static readonly StyledProperty<IEnumerable<string>> IncludedCategoriesProperty =
        AvaloniaProperty.Register<BetterPropertyGrid, IEnumerable<string>>("IncludedCategories");

    public IEnumerable<string> IncludedCategories
    {
        get => GetValue(IncludedCategoriesProperty);
        set => SetValue(IncludedCategoriesProperty, value);
    }

    static BetterPropertyGrid()
    {
        // Register factories
        CellEditFactoryService.Default.AddFactory(new ToggleSwitchCellEditFactory());

        // Initialize localization and name resolver
        LocalizationService.Default.AddExtraService(new PropertyGridLocalizationService());

        ExcludedCategoriesProperty.Changed.AddClassHandler<BetterPropertyGrid>(
            (grid, args) =>
            {
                if (args.NewValue is IEnumerable<string> excludedCategories)
                {
                    grid.FilterExcludeCategories(excludedCategories);
                }
            }
        );

        IncludedCategoriesProperty.Changed.AddClassHandler<BetterPropertyGrid>(
            (grid, args) =>
            {
                if (args.NewValue is IEnumerable<string> includedCategories)
                {
                    grid.FilterIncludeCategories(includedCategories);
                }
            }
        );
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is null)
            return;

        SetViewModelContext(DataContext);

        // Apply filters again
        FilterExcludeCategories(ExcludedCategories);
        FilterIncludeCategories(IncludedCategories);
    }

    public void FilterExcludeCategories(IEnumerable<string>? excludedCategories)
    {
        excludedCategories ??= [];

        if (DataContext is null)
            return;

        var categoryFilter = GetCategoryFilter();

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

    public void FilterIncludeCategories(IEnumerable<string>? includeCategories)
    {
        includeCategories ??= [];

        if (DataContext is null)
            return;

        var categoryFilter = GetCategoryFilter();

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

    private void SetViewModelContext(object? context)
    {
        // Get internal property `ViewModel` of internal type `PropertyGridViewModel`
        var propertyGridViewModelType =
            typeof(global::Avalonia.PropertyGrid.Controls.PropertyGrid).Assembly.GetType(
                "Avalonia.PropertyGrid.ViewModels.PropertyGridViewModel",
                true
            )!;

        var gridVm = this.GetProtectedProperty("ViewModel").Unwrap();

        // Set `Context` public property
        var contextProperty = propertyGridViewModelType
            .GetProperty("Context", BindingFlags.Instance | BindingFlags.Public)
            .Unwrap();
        contextProperty.SetValue(gridVm, context);

        // Trigger update that builds some stuff from `Context` and maybe initializes `Context` and `CategoryFilter`
        var buildPropertiesViewMethod = typeof(global::Avalonia.PropertyGrid.Controls.PropertyGrid)
            .GetMethod("BuildPropertiesView", BindingFlags.Instance | BindingFlags.NonPublic)
            .Unwrap();
        buildPropertiesViewMethod.Invoke(this, [DataContext, ShowStyle]);

        // Call this to ensure `CategoryFilter` is initialized
        var method = propertyGridViewModelType
            .GetMethod("RefreshProperties", BindingFlags.Instance | BindingFlags.Public)
            .Unwrap();
        method.Invoke(gridVm, null);
    }

    private CheckedMaskModel GetCategoryFilter()
    {
        // Get internal property `ViewModel` of internal type `PropertyGridViewModel`
        var propertyGridViewModelType =
            typeof(global::Avalonia.PropertyGrid.Controls.PropertyGrid).Assembly.GetType(
                "Avalonia.PropertyGrid.ViewModels.PropertyGridViewModel",
                true
            )!;

        var gridVm = this.GetProtectedProperty("ViewModel").Unwrap();

        // Call this to ensure `CategoryFilter` is initialized
        var method = propertyGridViewModelType
            .GetMethod("RefreshProperties", BindingFlags.Instance | BindingFlags.Public)
            .Unwrap();

        method.Invoke(gridVm, null);

        // Get public property `CategoryFilter`
        return gridVm.GetProtectedProperty<CheckedMaskModel>("CategoryFilter").Unwrap();
    }
}
