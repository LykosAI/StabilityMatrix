using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class EnvVarsDialog : UserControlBase
{
    public EnvVarsDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
