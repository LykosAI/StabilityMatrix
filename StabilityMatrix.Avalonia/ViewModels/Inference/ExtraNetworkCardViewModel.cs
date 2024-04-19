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

    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private double modelWeight = 1.0;

    [ObservableProperty]
    private bool isClipWeightEnabled;

    [ObservableProperty]
    private double clipWeight = 1.0;

    public IInferenceClientManager ClientManager { get; } = clientManager;
}
