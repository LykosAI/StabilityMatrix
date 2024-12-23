using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<PythonPackagesDialog>]
public partial class PythonPackagesDialog : UserControlBase
{
    public PythonPackagesDialog()
    {
        InitializeComponent();
    }
}
