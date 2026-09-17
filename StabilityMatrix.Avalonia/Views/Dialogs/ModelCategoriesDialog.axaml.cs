using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ModelCategoriesDialog>]
public partial class ModelCategoriesDialog : UserControlBase
{
    public ModelCategoriesDialog()
    {
        InitializeComponent();
    }
}
