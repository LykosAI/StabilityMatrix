using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<ProgressManagerPage>]
public partial class ProgressManagerPage : UserControlBase
{
    public ProgressManagerPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
