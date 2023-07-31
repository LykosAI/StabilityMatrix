using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceTextToImageView))]
public partial class InferenceTextToImageViewModel : ViewModelBase
{
    public SeedCardViewModel SeedCardViewModel { get; init; } = new();
    public SamplerCardViewModel SamplerCardViewModel { get; init; } = new();
}
