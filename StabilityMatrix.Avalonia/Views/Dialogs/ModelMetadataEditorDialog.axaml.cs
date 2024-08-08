using Avalonia.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Transient]
public partial class ModelMetadataEditorDialog : DropTargetUserControlBase
{
    public ModelMetadataEditorDialog()
    {
        InitializeComponent();
    }
}
