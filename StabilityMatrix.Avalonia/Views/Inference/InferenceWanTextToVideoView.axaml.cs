using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceWanTextToVideoView>]
public partial class InferenceWanTextToVideoView : DockUserControlBase
{
    public InferenceWanTextToVideoView()
    {
        InitializeComponent();
    }
}
