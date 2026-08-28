using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceTextToVideoView>]
public partial class InferenceTextToVideoView : DockUserControlBase
{
    public InferenceTextToVideoView()
    {
        InitializeComponent();
    }
}
