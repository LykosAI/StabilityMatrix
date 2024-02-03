using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Singleton]
public partial class InstallerDialog : UserControlBase
{
    public InstallerDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
