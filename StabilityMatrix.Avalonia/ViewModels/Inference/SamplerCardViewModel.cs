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
    [Required]
    private ComfyScheduler? selectedScheduler = ComfyScheduler.Normal;

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
            modulesCard.AvailableModules = [typeof(FreeUModule), typeof(ControlNetModule)];
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
        e.Temp.Conditioning = e.Builder.Connections.Base.Conditioning;
        e.Temp.RefinerConditioning = e.Builder.Connections.Refiner.Conditioning;

        // Apply steps from our addons
        ApplyAddonSteps(e);

        // If "Sampler" is not yet a node, do initial setup
        // otherwise do hires setup

        if (!e.Nodes.ContainsKey("Sampler"))
        {
            ApplyStepsInitialSampler(e);
        }
        else
        {
            ApplyStepsAdditionalSampler(e);
        }
    }

    private void ApplyStepsInitialSampler(ModuleApplyStepEventArgs e)
    {
        // Get primary as latent using vae
        var primaryLatent = e.Builder.GetPrimaryAsLatent();

        // Set primary sampler and scheduler
        var primarySampler = SelectedSampler ?? throw new ValidationException("Sampler not selected");
        e.Builder.Connections.PrimarySampler = primarySampler;

        var primaryScheduler = SelectedScheduler ?? throw new ValidationException("Scheduler not selected");
        e.Builder.Connections.PrimaryScheduler = primaryScheduler;

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
                    Cfg = CfgScale,
                    Positive = e.Temp.Conditioning?.Positive!,
                    Negative = e.Temp.Conditioning?.Negative!,
                    Sampler = kSamplerSelect.Output,
                    Sigmas = turboScheduler.Output,
                    LatentImage = primaryLatent
                }
            );

            e.Builder.Connections.Primary = sampler.Output1;

            return;
        }

        // Use KSampler if no refiner, otherwise need KSamplerAdvanced
        if (e.Builder.Connections.Refiner.Model is null)
        {
            var baseConditioning = e.Builder.Connections.Base.Conditioning.Unwrap();

            // No refiner
            var sampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSampler
                {
                    Name = "Sampler",
                    Model = e.Builder.Connections.Base.Model.Unwrap(),
                    Seed = e.Builder.Connections.Seed,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Steps = Steps,
                    Cfg = CfgScale,
                    Positive = baseConditioning.Positive,
                    Negative = baseConditioning.Negative,
                    LatentImage = primaryLatent,
                    Denoise = DenoiseStrength,
                }
            );

            e.Builder.Connections.Primary = sampler.Output;
        }
        else
        {
            var baseConditioning = e.Builder.Connections.Base.Conditioning.Unwrap();
            var refinerConditioning = e.Builder.Connections.Refiner.Conditioning.Unwrap();

            // Advanced base sampler for refiner
            var sampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSamplerAdvanced
                {
                    Name = "Sampler",
                    Model = e.Builder.Connections.Base.Model.Unwrap(),
                    AddNoise = true,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Steps = TotalSteps,
                    Cfg = CfgScale,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Positive = baseConditioning.Positive,
                    Negative = baseConditioning.Negative,
                    LatentImage = primaryLatent,
                    StartAtStep = 0,
                    EndAtStep = Steps,
                    ReturnWithLeftoverNoise = true
                }
            );

            // Add refiner sampler
            var refinerSampler = e.Nodes.AddTypedNode(
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
                    Positive = refinerConditioning.Positive,
                    Negative = refinerConditioning.Negative,
                    // Connect to previous sampler
                    LatentImage = sampler.Output,
                    StartAtStep = Steps,
                    EndAtStep = TotalSteps,
                    ReturnWithLeftoverNoise = false
                }
            );

            e.Builder.Connections.Primary = refinerSampler.Output;
        }
    }

    private void ApplyStepsAdditionalSampler(ModuleApplyStepEventArgs e) { }

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
