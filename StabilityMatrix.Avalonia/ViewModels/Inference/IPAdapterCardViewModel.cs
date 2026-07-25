using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(IPAdapterCard))]
[ManagedService]
[RegisterTransient<IPAdapterCardViewModel>]
public partial class IPAdapterCardViewModel : LoadableViewModelBase
{
    public const string ModuleKey = "IPAdapter";

    [ObservableProperty]
    [Required]
    public partial HybridModelFile? SelectedModel { get; set; }

    [ObservableProperty]
    [Required]
    public partial HybridModelFile? SelectedClipVisionModel { get; set; }

    [ObservableProperty]
    [Required]
    [Range(-1d, 5d)]
    public partial double Weight { get; set; } = 1.0;

    [ObservableProperty]
    [Required]
    [Localizable(false)]
    public partial string SelectedWeightType { get; set; } = "linear";

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    public partial double StartAt { get; set; }

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    public partial double EndAt { get; set; } = 1.0;

    /// <summary>
    /// Weight types supported by the IPAdapterAdvanced node
    /// </summary>
    [Localizable(false)]
    public IReadOnlyList<string> WeightTypes { get; } =
        [
            "linear",
            "ease in",
            "ease out",
            "ease in-out",
            "reverse in-out",
            "weak input",
            "weak output",
            "weak middle",
            "strong middle",
            "style transfer",
            "composition",
            "strong style transfer",
            "style and composition",
            "style transfer precise",
            "composition precise",
        ];

    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    public IInferenceClientManager ClientManager { get; }

    public IPAdapterCardViewModel(
        IInferenceClientManager clientManager,
        IServiceManager<ViewModelBase> vmFactory
    )
    {
        ClientManager = clientManager;
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();
    }
}
