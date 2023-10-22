using StabilityMatrix.Avalonia.Controls.Dock;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Inference;

[Transient]
public partial class InferenceTextToImageView : DockUserControlBase
{
    public InferenceTextToImageView()
    {
        InitializeComponent();
    }
}
