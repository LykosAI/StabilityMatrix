using Avalonia.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ModelMetadataEditorDialog>]
public partial class ModelMetadataEditorDialog : DropTargetUserControlBase
{
    public ModelMetadataEditorDialog()
    {
        InitializeComponent();
    }
}
