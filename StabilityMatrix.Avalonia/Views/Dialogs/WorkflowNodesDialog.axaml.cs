using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<WorkflowNodesDialog>]
public partial class WorkflowNodesDialog : UserControlBase
{
    public WorkflowNodesDialog()
    {
        InitializeComponent();
    }
}
