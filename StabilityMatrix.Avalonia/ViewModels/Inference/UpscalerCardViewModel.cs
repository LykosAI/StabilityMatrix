using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(UpscalerCard))]
public partial class UpscalerCardViewModel : ViewModelBase
{
    [ObservableProperty] private double scale = 1;
}
