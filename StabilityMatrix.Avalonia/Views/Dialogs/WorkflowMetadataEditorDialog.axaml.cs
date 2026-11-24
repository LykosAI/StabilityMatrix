using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<WorkflowMetadataEditorDialog>]
public partial class WorkflowMetadataEditorDialog : UserControlBase
{
    public WorkflowMetadataEditorDialog()
    {
        InitializeComponent();
    }
}
