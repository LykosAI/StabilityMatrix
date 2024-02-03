using Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.PackageManager;

[Transient]
public partial class PackageExtensionBrowserView : UserControlBase
{
    public PackageExtensionBrowserView()
    {
        InitializeComponent();
    }

    private void TabControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Clear selection when switching tabs
        if (DataContext is PackageExtensionBrowserViewModel vm)
        {
            vm.ClearSelection();
        }
    }
}
