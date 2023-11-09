using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(FreeUCard))]
[ManagedService]
[Transient]
public partial class FreeUCardViewModel : LoadableViewModelBase
{
    [ObservableProperty]
    [Required]
    [Range(0D, 10D)]
    private double b1 = 1.1;

    [ObservableProperty]
    [Required]
    [Range(0D, 10D)]
    private double b2 = 1.2;

    [ObservableProperty]
    [Required]
    [Range(0D, 10D)]
    private double s1 = 0.9;

    [ObservableProperty]
    [Required]
    [Range(0D, 10D)]
    private double s2 = 0.2;
}
