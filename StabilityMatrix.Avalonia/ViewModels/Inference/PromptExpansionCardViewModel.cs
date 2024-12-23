using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(PromptExpansionCard))]
[ManagedService]
[RegisterTransient<PromptExpansionCardViewModel>]
public partial class PromptExpansionCardViewModel(IInferenceClientManager clientManager)
    : LoadableViewModelBase
{
    public const string ModuleKey = "PromptExpansion";

    public IInferenceClientManager ClientManager { get; } = clientManager;

    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private bool isLogOutputEnabled = true;
}
