using Avalonia.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;

namespace StabilityMatrix.Avalonia.Views.PackageManager;

[RegisterTransient<PackageExtensionBrowserView>]
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
