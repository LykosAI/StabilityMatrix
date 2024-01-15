using Avalonia.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.PackageManager;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.PackageManager;

[Singleton]
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
