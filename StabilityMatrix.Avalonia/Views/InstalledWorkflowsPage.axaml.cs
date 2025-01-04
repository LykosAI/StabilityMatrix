using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<InstalledWorkflowsPage>]
public partial class InstalledWorkflowsPage : UserControlBase
{
    public InstalledWorkflowsPage()
    {
        InitializeComponent();
    }
}
