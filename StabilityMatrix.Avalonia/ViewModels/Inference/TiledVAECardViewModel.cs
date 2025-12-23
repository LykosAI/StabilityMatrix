using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(TiledVAECard))]
[ManagedService]
[RegisterTransient<TiledVAECardViewModel>]
public partial class TiledVAECardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "TiledVAE";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(64, 4096)]
    private int tileSize = 512;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0, 4096)]
    private int overlap = 64;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(8, 4096)]
    private int temporalSize = 64;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(4, 4096)]
    private int temporalOverlap = 8;
}
