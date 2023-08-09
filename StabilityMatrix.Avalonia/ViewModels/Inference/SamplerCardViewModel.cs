using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
public partial class SamplerCardViewModel : ViewModelBase, ILoadableState<SamplerCardModel>
{
    [ObservableProperty] private int steps = 20;

    [ObservableProperty] private bool isDenoiseStrengthEnabled = true;
    [ObservableProperty] private double denoiseStrength = 1;
    
    [ObservableProperty] private bool isCfgScaleEnabled = true;
    [ObservableProperty] private double cfgScale = 7;
    
    [ObservableProperty] private bool isDimensionsEnabled;
    [ObservableProperty] private int width = 512;
    [ObservableProperty] private int height = 512;
    
    [ObservableProperty] private bool isSamplerSelectionEnabled = true;
    
    [ObservableProperty, Required]
    private ComfySampler? selectedSampler;
    
    public IInferenceClientManager ClientManager { get; }

    public SamplerCardViewModel(IInferenceClientManager clientManager)
    {
        ClientManager = clientManager;
    }

    /// <inheritdoc />
    public void LoadState(SamplerCardModel state)
    {
        Steps = state.Steps;
        IsDenoiseStrengthEnabled = state.IsDenoiseStrengthEnabled;
        DenoiseStrength = state.DenoiseStrength;
        IsCfgScaleEnabled = state.IsCfgScaleEnabled;
        CfgScale = state.CfgScale;
        IsDimensionsEnabled = state.IsDimensionsEnabled;
        Width = state.Width;
        Height = state.Height;
        IsSamplerSelectionEnabled = state.IsSamplerSelectionEnabled;
        SelectedSampler = state.SelectedSampler is null ? null 
            : new ComfySampler(state.SelectedSampler);
    }

    /// <inheritdoc />
    public SamplerCardModel SaveState()
    {
        return new SamplerCardModel
        {
            Steps = Steps,
            IsDenoiseStrengthEnabled = IsDenoiseStrengthEnabled,
            DenoiseStrength = DenoiseStrength,
            IsCfgScaleEnabled = IsCfgScaleEnabled,
            CfgScale = CfgScale,
            IsDimensionsEnabled = IsDimensionsEnabled,
            Width = Width,
            Height = Height,
            IsSamplerSelectionEnabled = IsSamplerSelectionEnabled,
            SelectedSampler = SelectedSampler?.Name
        };
    }
}
