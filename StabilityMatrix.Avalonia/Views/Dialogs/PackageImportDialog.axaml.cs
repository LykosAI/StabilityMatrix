using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class PackageImportDialog : UserControlBase
{
    public PackageImportDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
