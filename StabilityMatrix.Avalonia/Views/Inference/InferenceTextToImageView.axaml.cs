using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceTextToImageView>]
public partial class InferenceTextToImageView : DockUserControlBase
{
    public InferenceTextToImageView()
    {
        InitializeComponent();
    }
}
