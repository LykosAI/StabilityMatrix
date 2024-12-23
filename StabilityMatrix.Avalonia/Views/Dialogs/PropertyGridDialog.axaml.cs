using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<PropertyGridDialog>]
public partial class PropertyGridDialog : UserControlBase
{
    public PropertyGridDialog()
    {
        InitializeComponent();
    }
}
