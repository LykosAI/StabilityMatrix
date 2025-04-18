using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SharpenCard))]
[ManagedService]
[RegisterScoped<SharpenCardViewModel>]
public partial class SharpenCardViewModel : LoadableViewModelBase
{
    [Range(1, 31)]
    [ObservableProperty]
    private int sharpenRadius = 1;

    [Range(0.1, 10)]
    [ObservableProperty]
    private double sigma = 1;

    [Range(0, 5)]
    [ObservableProperty]
    private double alpha = 1;
}
