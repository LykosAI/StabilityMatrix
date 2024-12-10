using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<WorkflowsPage>]
public partial class WorkflowsPage : UserControlBase
{
    public WorkflowsPage()
    {
        InitializeComponent();
    }
}
