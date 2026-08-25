using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceSvdImageToVideoView>]
public partial class InferenceSvdImageToVideoView : DockUserControlBase
{
    public InferenceSvdImageToVideoView()
    {
        InitializeComponent();
    }
}
