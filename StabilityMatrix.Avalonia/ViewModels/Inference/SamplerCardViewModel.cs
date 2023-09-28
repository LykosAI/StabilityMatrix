using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
public partial class SamplerCardViewModel : LoadableViewModelBase, IParametersLoadableState
{
    [ObservableProperty]
    private bool isRefinerStepsEnabled;

    [ObservableProperty]
    private int steps = 20;

    [ObservableProperty]
    private int refinerSteps = 10;

    [ObservableProperty]
    private bool isDenoiseStrengthEnabled;

    [ObservableProperty]
    private double denoiseStrength = 1;

    [ObservableProperty]
    private bool isCfgScaleEnabled;

    [ObservableProperty]
    private double cfgScale = 7;

    [ObservableProperty]
    private bool isDimensionsEnabled;

    [ObservableProperty]
    private int width = 512;

    [ObservableProperty]
    private int height = 512;

    [ObservableProperty]
    private bool isSamplerSelectionEnabled;

    [ObservableProperty]
    private ComfySampler? selectedSampler = new ComfySampler("euler_ancestral");

    [ObservableProperty]
    private bool isSchedulerSelectionEnabled;

    [ObservableProperty]
    private ComfyScheduler? selectedScheduler = new ComfyScheduler("normal");

    [JsonIgnore]
    public IInferenceClientManager ClientManager { get; }

    public SamplerCardViewModel(IInferenceClientManager clientManager)
    {
        ClientManager = clientManager;
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        Width = parameters.Width;
        Height = parameters.Height;
        Steps = parameters.Steps;
        CfgScale = parameters.CfgScale;

        if (parameters.GetComfySamplers() is { } paramSamplers)
        {
            var (sampler, scheduler) = paramSamplers;

            SelectedSampler = ClientManager.Samplers.FirstOrDefault(s => s.Name == sampler.Name);
        }
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            Width = Width,
            Height = Height,
            Steps = Steps,
            CfgScale = CfgScale,
            Sampler = SelectedSampler?.Name
        };
    }
}
