using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
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
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
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
    private double denoiseStrength = 1;

    [ObservableProperty]
    [property: Category("Settings")]
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
    private bool isSamplerSelectionEnabled;

    [ObservableProperty]
    [Required]
    private ComfySampler? selectedSampler = ComfySampler.EulerAncestral;

    [ObservableProperty]
    [property: Category("Settings")]
    private bool isSchedulerSelectionEnabled;

    [ObservableProperty]
    [Required]
    private ComfyScheduler? selectedScheduler = ComfyScheduler.Normal;

    [JsonPropertyName("Modules")]
    public StackEditableCardViewModel ModulesCardViewModel { get; }

    [JsonIgnore]
    public IInferenceClientManager ClientManager { get; }

    private int TotalSteps => Steps + RefinerSteps;

    public SamplerCardViewModel(IInferenceClientManager clientManager, ServiceManager<ViewModelBase> vmFactory)
    {
        ClientManager = clientManager;
        ModulesCardViewModel = vmFactory.Get<StackEditableCardViewModel>(modulesCard =>
        {
            modulesCard.Title = Resources.Label_Addons;
            modulesCard.AvailableModules = new[] { typeof(FreeUModule), typeof(ControlNetModule) };
        });
    }

    /// <inheritdoc />
    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        // Resample the current primary if size does not match the selected size
        if (e.Builder.Connections.PrimarySize.Width != Width || e.Builder.Connections.PrimarySize.Height != Height)
        {
            e.Builder.Connections.Primary = e.Builder.Group_Upscale(
                e.Nodes.GetUniqueName("Sampler_ScalePrimary"),
                e.Builder.Connections.Primary ?? throw new ArgumentException("No Primary"),
                e.Builder.Connections.GetDefaultVAE(),
                ComfyUpscaler.NearestExact,
                Width,
                Height
            );
        }

        // Provide temp values
        e.Temp.Conditioning = (
            e.Builder.Connections.GetRefinerOrBaseConditioning(),
            e.Builder.Connections.GetRefinerOrBaseNegativeConditioning()
        );
        e.Temp.RefinerConditioning = (
            e.Builder.Connections.RefinerConditioning!,
            e.Builder.Connections.RefinerNegativeConditioning!
        );

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
        // Get primary or base VAE
        var vae =
            e.Builder.Connections.PrimaryVAE
            ?? e.Builder.Connections.BaseVAE
            ?? throw new ArgumentException("No Primary or Base VAE");

        // Get primary as latent using vae
        var primaryLatent = e.Builder.GetPrimaryAsLatent(vae);

        // Set primary sampler and scheduler
        e.Builder.Connections.PrimarySampler = SelectedSampler ?? throw new ValidationException("Sampler not selected");
        e.Builder.Connections.PrimaryScheduler =
            SelectedScheduler ?? throw new ValidationException("Scheduler not selected");

        // Use KSampler if no refiner, otherwise need KSamplerAdvanced
        if (e.Builder.Connections.RefinerModel is null)
        {
            // No refiner
            var sampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSampler
                {
                    Name = "Sampler",
                    Model = e.Builder.Connections.BaseModel ?? throw new ArgumentException("No BaseModel"),
                    Seed = e.Builder.Connections.Seed,
                    SamplerName = e.Builder.Connections.PrimarySampler?.Name!,
                    Scheduler = e.Builder.Connections.PrimaryScheduler?.Name!,
                    Steps = Steps,
                    Cfg = CfgScale,
                    Positive = e.Temp.Conditioning?.Positive!,
                    Negative = e.Temp.Conditioning?.Negative!,
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
                    Model = e.Builder.Connections.BaseModel ?? throw new ArgumentException("No BaseModel"),
                    AddNoise = true,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Steps = TotalSteps,
                    Cfg = CfgScale,
                    SamplerName = e.Builder.Connections.PrimarySampler?.Name!,
                    Scheduler = e.Builder.Connections.PrimaryScheduler?.Name!,
                    Positive = e.Temp.Conditioning?.Positive!,
                    Negative = e.Temp.Conditioning?.Negative!,
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
                    Name = "Refiner_Sampler",
                    Model = e.Builder.Connections.RefinerModel ?? throw new ArgumentException("No RefinerModel"),
                    AddNoise = false,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Steps = TotalSteps,
                    Cfg = CfgScale,
                    SamplerName = e.Builder.Connections.PrimarySampler?.Name!,
                    Scheduler = e.Builder.Connections.PrimaryScheduler?.Name!,
                    Positive = e.Temp.RefinerConditioning?.Positive!,
                    Negative = e.Temp.RefinerConditioning?.Negative!,
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
            && GenerationParametersConverter.TryGetSamplerScheduler(parameters.Sampler, out var samplerScheduler)
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
