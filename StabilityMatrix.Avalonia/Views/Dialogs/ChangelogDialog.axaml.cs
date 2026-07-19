using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ChangelogDialog>]
public partial class ChangelogDialog : UserControlBase
{
    public ChangelogDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
