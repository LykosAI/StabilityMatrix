using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(RescaleCfgCard))]
[ManagedService]
[RegisterTransient<RescaleCfgCardViewModel>]
public partial class RescaleCfgCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "RescaleCFG";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0d, 1d)]
    private double multiplier = 0.7d;
}
