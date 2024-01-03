using Avalonia.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Singleton]
public partial class NewInstallerDialog : UserControlBase
{
    public NewInstallerDialog()
    {
        InitializeComponent();
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is NewInstallerDialogViewModel vm)
        {
            vm.ClearSearchQuery();
        }
    }
}
