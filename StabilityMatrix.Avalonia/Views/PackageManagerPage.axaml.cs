using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

public partial class PackageManagerPage : UserControlBase
{
    public PackageManagerPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
