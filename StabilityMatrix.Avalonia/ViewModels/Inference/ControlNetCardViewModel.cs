using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ControlNetCard))]
public partial class ControlNetCardViewModel : LoadableViewModelBase
{
    [ObservableProperty]
    [Required]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    [Required]
    private HybridModelFile? selectedPreprocessor;

    [ObservableProperty]
    [Required]
    [Range(0d, 10d)]
    private double strength = 1.0;

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    private double startPercent;

    [ObservableProperty]
    [Required]
    [Range(0d, 1d)]
    private double endPercent = 1.0;

    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    public IInferenceClientManager ClientManager { get; }

    public ControlNetCardViewModel(
        IInferenceClientManager clientManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        ClientManager = clientManager;
        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();
    }
}
