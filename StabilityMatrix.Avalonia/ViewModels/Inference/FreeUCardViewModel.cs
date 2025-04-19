using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(FreeUCard))]
[ManagedService]
[RegisterTransient<FreeUCardViewModel>]
public partial class FreeUCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "FreeU";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0D, 10D)]
    private double b1 = 1.5;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0D, 10D)]
    private double b2 = 1.6;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0D, 10D)]
    private double s1 = 0.9;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0D, 10D)]
    private double s2 = 0.2;
}
