using Avalonia.Collections;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceConfigCard))]
public class InferenceConfigCardViewModel : ViewModelBase
{
    public AvaloniaList<ViewModelBase> ConfigCards { get; } = new();
}
