using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(NrsCard))]
[ManagedService]
[RegisterTransient<NrsCardViewModel>]
public partial class NrsCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "NRS";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(-30.0d, 30.0d)]
    public partial double Skew { get; set; } = 4;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(-30.0d, 30.0d)]
    public partial double Stretch { get; set; } = 2;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0d, 1d)]
    public partial double Squash { get; set; } = 0;
}
