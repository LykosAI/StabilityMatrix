using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

/// <summary>
/// Settings for <see cref="Modules.SkipLayerGuidanceModule"/>. Defaults are MOSS Image's
/// (single block 22 for the first half of sampling), from Comfy-Org's MOSS Image template.
/// </summary>
[View(typeof(SkipLayerGuidanceCard))]
[ManagedService]
[RegisterTransient<SkipLayerGuidanceCardViewModel>]
public partial class SkipLayerGuidanceCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "SkipLayerGuidance";

    /// <summary>Comma-separated double-stream block indices.</summary>
    [ObservableProperty]
    public partial string DoubleLayers { get; set; } = string.Empty;

    /// <summary>Comma-separated single-stream block indices.</summary>
    [ObservableProperty]
    public partial string SingleLayers { get; set; } = "22";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0d, 1d)]
    public partial double StartPercent { get; set; }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [Range(0d, 1d)]
    public partial double EndPercent { get; set; } = 0.5;
}
