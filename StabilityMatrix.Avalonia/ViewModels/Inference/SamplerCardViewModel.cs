using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
[ManagedService]
[Transient]
public partial class SamplerCardViewModel
    : LoadableViewModelBase,
        IParametersLoadableState,
        IComfyStep
{
    public const string ModuleKey = "Sampler";

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
    [Required]
    private ComfySampler? selectedSampler = ComfySampler.EulerAncestral;

    [ObservableProperty]
    private bool isSchedulerSelectionEnabled;

    [ObservableProperty]
    [Required]
    private ComfyScheduler? selectedScheduler = ComfyScheduler.Normal;

    [JsonPropertyName("Modules")]
    public StackEditableCardViewModel ModulesCardViewModel { get; }

    [JsonIgnore]
    public IInferenceClientManager ClientManager { get; }

    public SamplerCardViewModel(
        IInferenceClientManager clientManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        ClientManager = clientManager;
        ModulesCardViewModel = vmFactory.Get<StackEditableCardViewModel>(modulesCard =>
        {
            modulesCard.Title = "Addons";
            modulesCard.AvailableModules = new[] { typeof(FreeUModule), typeof(ControlNetModule) };
            modulesCard.InitializeDefaults();
        });

        ModulesCardViewModel.CardAdded += (
            (sender, item) =>
            {
                if (item is ControlNetModule module)
                {
                    // Inherit our edit state
                    // module.IsEditEnabled = IsEditEnabled;
                }
            }
        );
    }

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        // Apply steps from our modules
        foreach (var module in ModulesCardViewModel.Cards.Cast<ModuleBase>())
        {
            module.ApplyStep(e);
        }
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        Width = parameters.Width;
        Height = parameters.Height;
        Steps = parameters.Steps;
        CfgScale = parameters.CfgScale;

        if (
            !string.IsNullOrEmpty(parameters.Sampler)
            && GenerationParametersConverter.TryGetSamplerScheduler(
                parameters.Sampler,
                out var samplerScheduler
            )
        )
        {
            SelectedSampler = ClientManager.Samplers.FirstOrDefault(
                s => s == samplerScheduler.Sampler
            );
            SelectedScheduler = ClientManager.Schedulers.FirstOrDefault(
                s => s == samplerScheduler.Scheduler
            );
        }
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        var sampler = GenerationParametersConverter.TryGetParameters(
            new ComfySamplerScheduler(SelectedSampler ?? default, SelectedScheduler ?? default),
            out var res
        )
            ? res
            : null;
        return parameters with
        {
            Width = Width,
            Height = Height,
            Steps = Steps,
            CfgScale = CfgScale,
            Sampler = sampler,
        };
    }
}
