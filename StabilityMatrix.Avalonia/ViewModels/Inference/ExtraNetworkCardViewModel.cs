using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ExtraNetworkCard))]
[ManagedService]
[Transient]
public partial class ExtraNetworkCardViewModel(IInferenceClientManager clientManager) : LoadableViewModelBase
{
    public const string ModuleKey = "ExtraNetwork";

    /// <summary>
    /// Whether user can toggle model weight visibility
    /// </summary>
    [JsonIgnore]
    public bool IsModelWeightToggleEnabled { get; set; }

    /// <summary>
    /// Whether user can toggle clip weight visibility
    /// </summary>
    [JsonIgnore]
    public bool IsClipWeightToggleEnabled { get; set; }

    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private bool isModelWeightEnabled;

    [ObservableProperty]
    private bool isClipWeightEnabled;

    [ObservableProperty]
    private double modelWeight = 1.0;

    [ObservableProperty]
    private double clipWeight = 1.0;

    public IInferenceClientManager ClientManager { get; } = clientManager;
}
