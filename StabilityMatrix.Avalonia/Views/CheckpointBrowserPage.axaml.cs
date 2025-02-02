using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterSingleton<CheckpointBrowserPage>]
public partial class CheckpointBrowserPage : UserControlBase
{
    public CheckpointBrowserPage()
    {
        InitializeComponent();
    }
}
