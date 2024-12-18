using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceImageToImageView>]
public partial class InferenceImageToImageView : DockUserControlBase
{
    public InferenceImageToImageView()
    {
        InitializeComponent();
    }
}
