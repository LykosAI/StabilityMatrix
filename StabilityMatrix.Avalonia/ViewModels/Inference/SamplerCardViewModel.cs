using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
public partial class SamplerCardViewModel : ViewModelBase, ILoadableState<SamplerCardModel>
{
    [ObservableProperty] private int steps = 20;
    [ObservableProperty] private double cfgScale = 7;
    [ObservableProperty] private int width = 512;
    [ObservableProperty] private int height = 512;
    
    [ObservableProperty, Required]
    private string? selectedSampler;
    
    public IInferenceClientManager ClientManager { get; }

    public SamplerCardViewModel(IInferenceClientManager clientManager)
    {
        ClientManager = clientManager;
    }

    /// <inheritdoc />
    public void LoadState(SamplerCardModel state)
    {
        Steps = state.Steps;
        CfgScale = state.CfgScale;
        Width = state.Width;
        Height = state.Height;
        SelectedSampler = state.SelectedSampler;
    }

    /// <inheritdoc />
    public SamplerCardModel SaveState()
    {
        return new SamplerCardModel
        {
            Steps = Steps,
            CfgScale = CfgScale,
            Width = Width,
            Height = Height,
            SelectedSampler = SelectedSampler
        };
    }
}
