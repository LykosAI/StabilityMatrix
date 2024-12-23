using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<PythonPackageSpecifiersDialog>]
public partial class PythonPackageSpecifiersDialog : UserControlBase
{
    public PythonPackageSpecifiersDialog()
    {
        InitializeComponent();
    }
}
