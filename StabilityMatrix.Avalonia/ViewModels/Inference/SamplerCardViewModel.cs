using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
public partial class SamplerCardViewModel : ViewModelBase
{
    [ObservableProperty] private int steps;
    [ObservableProperty] private double cfgScale;
    [ObservableProperty] private int width;
    [ObservableProperty] private int height;
    
    [ObservableProperty] private string? selectedSampler;
    [ObservableProperty] private IReadOnlyList<string> samplers = new List<string>();
}
