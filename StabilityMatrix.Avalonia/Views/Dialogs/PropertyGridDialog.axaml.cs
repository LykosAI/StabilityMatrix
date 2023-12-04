using Avalonia.Controls.Primitives;
using Avalonia.PropertyGrid.Controls;
using Avalonia.PropertyGrid.ViewModels;
using PropertyModels.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Transient]
public partial class PropertyGridDialog : UserControlBase
{
    public PropertyGridDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var vm = DataContext as PropertyGridViewModel;

        if (vm?.ExcludeCategories is { } excludeCategories)
        {
            MainPropertyGrid.FilterExcludeCategories(excludeCategories);
        }

        if (vm?.IncludeCategories is { } includeCategories)
        {
            MainPropertyGrid.FilterIncludeCategories(includeCategories);
        }
    }

    internal class CustomFilter : IPropertyGridFilterContext
    {
        /// <inheritdoc />
        public IFilterPattern? FilterPattern => null;

        /// <inheritdoc />
        public ICheckedMaskModel? FastFilterPattern => null;

        /// <inheritdoc />
        public PropertyVisibility PropagateVisibility(
            IPropertyGridCellInfo info,
            FilterCategory category = FilterCategory.Default
        )
        {
            return PropertyVisibility.AlwaysVisible;
        }
    }
}
