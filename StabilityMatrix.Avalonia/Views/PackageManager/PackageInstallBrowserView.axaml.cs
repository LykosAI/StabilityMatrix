using Avalonia.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;

namespace StabilityMatrix.Avalonia.Views.PackageManager;

[RegisterSingleton<PackageInstallBrowserView>]
public partial class PackageInstallBrowserView : UserControlBase
{
    public PackageInstallBrowserView()
    {
        InitializeComponent();
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is PackageInstallBrowserViewModel vm)
        {
            vm.ClearSearchQuery();
        }
    }
}
