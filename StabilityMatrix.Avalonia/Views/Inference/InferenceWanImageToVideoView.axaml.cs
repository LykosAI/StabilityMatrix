using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceWanImageToVideoView>]
public partial class InferenceWanImageToVideoView : DockUserControlBase
{
    public InferenceWanImageToVideoView()
    {
        InitializeComponent();
    }
}
