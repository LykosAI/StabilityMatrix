using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls.Dock;

namespace StabilityMatrix.Avalonia.Views.Inference;

[RegisterTransient<InferenceImageUpscaleView>]
public partial class InferenceImageUpscaleView : DockUserControlBase
{
    public InferenceImageUpscaleView()
    {
        InitializeComponent();
    }
}
