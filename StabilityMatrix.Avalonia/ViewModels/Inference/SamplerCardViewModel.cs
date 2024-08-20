using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using Size = System.Drawing.Size;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
[ManagedService]
[Transient]
public partial class SamplerCardViewModel : LoadableViewModelBase, IParametersLoadableState, IComfyStep
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

    /// <summary>
    /// Temporary enable for denoise strength, used for SDTurbo.
    /// Denoise will be enabled if either this or <see cref="IsDenoiseStrengthEnabled"/> is true.
    /// </summary>
    public bool IsDenoiseStrengthTempEnabled => SelectedScheduler == ComfyScheduler.SDTurbo;

    [ObservableProperty]
    private double denoiseStrength = 0.7f;

    [ObservableProperty]
    [property: Category("Settings")]
    [property: DisplayName("CFG Scale Selection")]
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
    [property: Category("Settings")]
    [property: DisplayName("Sampler Selection")]
    private bool isSamplerSelectionEnabled;

    [ObservableProperty]
    [Required]
    private ComfySampler? selectedSampler = ComfySampler.EulerAncestral;

    [ObservableProperty]
    [property: Category("Settings")]
    [property: DisplayName("Scheduler Selection")]
    private bool isSchedulerSelectionEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDenoiseStrengthTempEnabled))]
    [Required]
    private ComfyScheduler? selectedScheduler = ComfyScheduler.Normal;

    [ObservableProperty]
    [property: Category("Settings")]
    [property: DisplayName("Inherit Primary Sampler Addons")]
    private bool inheritPrimarySamplerAddons = true;

    [ObservableProperty]
    private bool enableAddons = true;

    [JsonPropertyName("Modules")]
    public StackEditableCardViewModel ModulesCardViewModel { get; }

    [JsonIgnore]
    public IInferenceClientManager ClientManager { get; }

    private int TotalSteps => Steps + RefinerSteps;

    public SamplerCardViewModel(
        IInferenceClientManager clientManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        ClientManager = clientManager;
        ModulesCardViewModel = vmFactory.Get<StackEditableCardViewModel>(modulesCard =>
        {
            modulesCard.Title = Resources.Label_Addons;
            modulesCard.AvailableModules =
            [
                typeof(FreeUModule),
                typeof(ControlNetModule),
                typeof(LayerDiffuseModule),
                typeof(FluxGuidanceModule)
            ];
        });
    }

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        // Resample the current primary if size does not match the selected size
        if (
            e.Builder.Connections.PrimarySize.Width != Width
            || e.Builder.Connections.PrimarySize.Height != Height
        )
        {
            e.Builder.Connections.Primary = e.Builder.Group_Upscale(
                e.Nodes.GetUniqueName("Sampler_ScalePrimary"),
                e.Builder.Connections.Primary ?? throw new ArgumentException("No Primary"),
                e.Builder.Connections.GetDefaultVAE(),
                ComfyUpscaler.NearestExact,
                Width,
                Height
            );

            e.Builder.Connections.PrimarySize = new Size(Width, Height);
        }

        // Provide temp values
        e.Temp = e.CreateTempFromBuilder();

        // Apply steps from our addons
        ApplyAddonSteps(e);

        // If "Sampler" is not yet a node, do initial setup
        // otherwise do hires setup

        if (!e.Nodes.ContainsKey("Sampler"))
        {
            ApplyStepsInitialSampler(e);

            // Save temp
            e.Builder.Connections.BaseSamplerTemporaryArgs = e.Temp;
        }
        else
        {
            // Hires does its own sampling so just throw I guess
            throw new InvalidOperationException(
                "Sampler ApplyStep was called when Sampler node already exists"
            );
        }
    }

    public void ApplyStepsInitialFluxSampler(ModuleApplyStepEventArgs e)
    {
        // Provide temp values
        e.Temp = e.CreateTempFromBuilder();

        // Get primary as latent using vae
        var primaryLatent = e.Builder.GetPrimaryAsLatent(
            e.Temp.Primary!.Unwrap(),
            e.Builder.Connections.GetDefaultVAE()
        );

        // Set primary sampler and scheduler
        var primarySampler = SelectedSampler ?? throw new ValidationException("Sampler not selected");
        e.Builder.Connections.PrimarySampler = primarySampler;

        var primaryScheduler = SelectedScheduler ?? throw new ValidationException("Scheduler not selected");
        e.Builder.Connections.PrimaryScheduler = primaryScheduler;

        // KSamplerSelect
        var kSamplerSelect = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.KSamplerSelect
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.KSamplerSelect)),
                SamplerName = e.Builder.Connections.PrimarySampler?.Name!
            }
        );

        e.Builder.Connections.PrimarySamplerNode = kSamplerSelect.Output;

        // Scheduler/Sigmas
        var basicScheduler = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.BasicScheduler
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.BasicScheduler)),
                Model = e.Builder.Connections.Base.Model.Unwrap(),
                Scheduler = e.Builder.Connections.PrimaryScheduler?.Name!,
                Denoise = IsDenoiseStrengthEnabled ? DenoiseStrength : 1.0d,
                Steps = Steps
            }
        );

        e.Builder.Connections.PrimarySigmas = basicScheduler.Output;

        // Noise
        var randomNoise = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.RandomNoise
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.RandomNoise)),
                NoiseSeed = e.Builder.Connections.Seed
            }
        );

        e.Builder.Connections.PrimaryNoise = randomNoise.Output;

        // Guidance
        var fluxGuidance = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.FluxGuidance
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.FluxGuidance)),
                Conditioning = e.Builder.Connections.GetRefinerOrBaseConditioning().Positive,
                Guidance = CfgScale
            }
        );

        e.Builder.Connections.Base.Conditioning = new ConditioningConnections(
            fluxGuidance.Output,
            e.Builder.Connections.GetRefinerOrBaseConditioning().Negative
        );

        // Guider
        var basicGuider = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.BasicGuider
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.BasicGuider)),
                Model = e.Builder.Connections.Base.Model.Unwrap(),
                Conditioning = e.Builder.Connections.GetRefinerOrBaseConditioning().Positive
            }
        );

        e.Builder.Connections.PrimaryGuider = basicGuider.Output;

        // SamplerCustomAdvanced
        var sampler = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.SamplerCustomAdvanced
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.SamplerCustomAdvanced)),
                Guider = e.Builder.Connections.PrimaryGuider,
                Noise = e.Builder.Connections.PrimaryNoise,
                Sampler = e.Builder.Connections.PrimarySamplerNode,
                Sigmas = e.Builder.Connections.PrimarySigmas,
                LatentImage = primaryLatent
            }
        );

        e.Builder.Connections.Primary = sampler.Output1;

        e.Builder.Connections.BaseSamplerTemporaryArgs = e.Temp;
    }

    private void ApplyStepsInitialSampler(ModuleApplyStepEventArgs e)
    {
        // Get primary as latent using vae
        var primaryLatent = e.Builder.GetPrimaryAsLatent(
            e.Temp.Primary!.Unwrap(),
            e.Builder.Connections.GetDefaultVAE()
        );

        // Set primary sampler and scheduler
        var primarySampler = SelectedSampler ?? throw new ValidationException("Sampler not selected");
        e.Builder.Connections.PrimarySampler = primarySampler;

        var primaryScheduler = SelectedScheduler ?? throw new ValidationException("Scheduler not selected");
        e.Builder.Connections.PrimaryScheduler = primaryScheduler;

        // Use Temp Conditioning that may be modified by addons
        var conditioning = e.Temp.Base.Conditioning.Unwrap();
        var refinerConditioning = e.Temp.Refiner.Conditioning;

        var useFluxGuidance = ModulesCardViewModel.IsModuleEnabled<FluxGuidanceModule>();

        if (useFluxGuidance)
        {
            // Flux guidance
            var fluxGuidance = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.FluxGuidance
                {
                    Name = e.Nodes.GetUniqueName("FluxGuidance"),
                    Conditioning = conditioning.Positive,
                    Guidance = CfgScale
                }
            );

            conditioning = conditioning with { Positive = fluxGuidance.Output };
        }

        // Use custom sampler if SDTurbo scheduler is selected
        if (e.Builder.Connections.PrimaryScheduler == ComfyScheduler.SDTurbo)
        {
            // Error if using refiner
            if (e.Builder.Connections.Refiner.Model is not null)
            {
                throw new ValidationException("SDTurbo Scheduler cannot be used with Refiner Model");
            }

            var kSamplerSelect = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSamplerSelect
                {
                    Name = "KSamplerSelect",
                    SamplerName = e.Builder.Connections.PrimarySampler?.Name!
                }
            );

            var turboScheduler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.SDTurboScheduler
                {
                    Name = "SDTurboScheduler",
                    Model = e.Builder.Connections.Base.Model.Unwrap(),
                    Steps = Steps,
                    Denoise = DenoiseStrength
                }
            );

            var sampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.SamplerCustom
                {
                    Name = "Sampler",
                    Model = e.Builder.Connections.Base.Model,
                    AddNoise = true,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Cfg = useFluxGuidance ? 1.0d : CfgScale,
                    Positive = conditioning.Positive,
                    Negative = conditioning.Negative,
                    Sampler = kSamplerSelect.Output,
                    Sigmas = turboScheduler.Output,
                    LatentImage = primaryLatent
                }
            );

            e.Builder.Connections.Primary = sampler.Output1;
        }
        // Use KSampler if no refiner, otherwise need KSamplerAdvanced
        else if (e.Builder.Connections.Refiner.Model is null)
        {
            // No refiner
            var sampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSampler
                {
                    Name = "Sampler",
                    Model = e.Temp.Base.Model!.Unwrap(),
                    Seed = e.Builder.Connections.Seed,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Steps = Steps,
                    Cfg = useFluxGuidance ? 1.0d : CfgScale,
                    Positive = conditioning.Positive,
                    Negative = conditioning.Negative,
                    LatentImage = primaryLatent,
                    Denoise = DenoiseStrength,
                }
            );

            e.Builder.Connections.Primary = sampler.Output;
        }
        else
        {
            // Advanced base sampler for refiner
            var sampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSamplerAdvanced
                {
                    Name = "Sampler",
                    Model = e.Temp.Base.Model!.Unwrap(),
                    AddNoise = true,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Steps = TotalSteps,
                    Cfg = useFluxGuidance ? 1.0d : CfgScale,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Positive = conditioning.Positive,
                    Negative = conditioning.Negative,
                    LatentImage = primaryLatent,
                    StartAtStep = 0,
                    EndAtStep = Steps,
                    ReturnWithLeftoverNoise = true
                }
            );

            e.Builder.Connections.Primary = sampler.Output;
        }

        // If temp batched, add a LatentFromBatch to pick the temp batch right after first sampler
        if (e.Temp.IsPrimaryTempBatched)
        {
            e.Builder.Connections.Primary = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.LatentFromBatch
                {
                    Name = e.Nodes.GetUniqueName("ControlNet_LatentFromBatch"),
                    Samples = e.Builder.GetPrimaryAsLatent(),
                    BatchIndex = e.Temp.PrimaryTempBatchPickIndex,
                    // Use max length here as recommended
                    // https://github.com/comfyanonymous/ComfyUI_experiments/issues/11
                    Length = 64
                }
            ).Output;
        }

        // Refiner
        if (e.Builder.Connections.Refiner.Model is not null)
        {
            // Add refiner sampler
            e.Builder.Connections.Primary = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSamplerAdvanced
                {
                    Name = "Sampler_Refiner",
                    Model = e.Builder.Connections.Refiner.Model,
                    AddNoise = false,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Steps = TotalSteps,
                    Cfg = CfgScale,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Positive = refinerConditioning!.Positive,
                    Negative = refinerConditioning.Negative,
                    // Connect to previous sampler
                    LatentImage = e.Builder.GetPrimaryAsLatent(),
                    StartAtStep = Steps,
                    EndAtStep = TotalSteps,
                    ReturnWithLeftoverNoise = false
                }
            ).Output;
        }
    }

    /// <summary>
    /// Applies each step of our addons
    /// </summary>
    /// <param name="e"></param>
    private void ApplyAddonSteps(ModuleApplyStepEventArgs e)
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
            SelectedSampler = ClientManager.Samplers.FirstOrDefault(s => s == samplerScheduler.Sampler);
            SelectedScheduler = ClientManager.Schedulers.FirstOrDefault(s => s == samplerScheduler.Scheduler);
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
