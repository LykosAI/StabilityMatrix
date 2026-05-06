using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<OrganizeModelsDialog>]
public partial class OrganizeModelsDialog : UserControlBase
{
    public OrganizeModelsDialog()
    {
        InitializeComponent();
    }
}
