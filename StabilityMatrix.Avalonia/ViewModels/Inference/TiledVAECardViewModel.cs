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

    // Spatial tile size (valid for Wan)
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(64, 4096)]
    private int tileSize = 512;

    // Spatial overlap
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0, 4096)]
    private int overlap = 64;

    // Toggle: Use custom temporal tiling settings
    [ObservableProperty]
    private bool useCustomTemporalTiling = false;

    // Temporal tile size (must be >= 8)
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(8, 4096)]
    private int temporalSize = 64;

    // Temporal overlap (must be >= 4)
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(4, 4096)]
    private int temporalOverlap = 8;
}
