using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceImageToVideoView>]
public partial class InferenceImageToVideoView : DockUserControlBase
{
    public InferenceImageToVideoView()
    {
        InitializeComponent();
    }
}
