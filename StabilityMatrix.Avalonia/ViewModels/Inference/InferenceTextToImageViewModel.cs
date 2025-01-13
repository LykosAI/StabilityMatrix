using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Binding;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Services;
using InferenceTextToImageView = StabilityMatrix.Avalonia.Views.Inference.InferenceTextToImageView;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceTextToImageView), IsPersistent = true)]
[ManagedService]
[RegisterTransient<InferenceTextToImageViewModel>]
public class InferenceTextToImageViewModel : InferenceGenerationViewModelBase, IParametersLoadableState
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;
    private readonly IModelIndexService modelIndexService;

    [JsonIgnore]
    public StackCardViewModel StackCardViewModel { get; }

    [JsonPropertyName("Modules")]
    public StackEditableCardViewModel ModulesCardViewModel { get; }

    [JsonPropertyName("Model")]
    public ModelCardViewModel ModelCardViewModel { get; }

    [JsonPropertyName("Sampler")]
    public SamplerCardViewModel SamplerCardViewModel { get; }

    [JsonPropertyName("Prompt")]
    public PromptCardViewModel PromptCardViewModel { get; }

    [JsonPropertyName("BatchSize")]
    public BatchSizeCardViewModel BatchSizeCardViewModel { get; }

    [JsonPropertyName("Seed")]
    public SeedCardViewModel SeedCardViewModel { get; }

    public InferenceTextToImageViewModel(
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> vmFactory,
        IModelIndexService modelIndexService,
        RunningPackageService runningPackageService
    )
        : base(vmFactory, inferenceClientManager, notificationService, settingsManager, runningPackageService)
    {
        this.notificationService = notificationService;
        this.modelIndexService = modelIndexService;

        // Get sub view models from service manager

        SeedCardViewModel = vmFactory.Get<SeedCardViewModel>();
        SeedCardViewModel.GenerateNewSeed();

        ModelCardViewModel = vmFactory.Get<ModelCardViewModel>();

        SamplerCardViewModel = vmFactory.Get<SamplerCardViewModel>(samplerCard =>
        {
            samplerCard.IsDimensionsEnabled = true;
            samplerCard.IsCfgScaleEnabled = true;
            samplerCard.IsSamplerSelectionEnabled = true;
            samplerCard.IsSchedulerSelectionEnabled = true;
            samplerCard.DenoiseStrength = 1.0d;
        });

        PromptCardViewModel = AddDisposable(vmFactory.Get<PromptCardViewModel>());

        BatchSizeCardViewModel = vmFactory.Get<BatchSizeCardViewModel>();

        ModulesCardViewModel = vmFactory.Get<StackEditableCardViewModel>(modulesCard =>
        {
            modulesCard.AvailableModules = new[]
            {
                typeof(HiresFixModule),
                typeof(UpscalerModule),
                typeof(SaveImageModule),
                typeof(FaceDetailerModule)
            };
            modulesCard.DefaultModules = new[] { typeof(HiresFixModule), typeof(UpscalerModule) };
            modulesCard.InitializeDefaults();
        });

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();
        StackCardViewModel.AddCards(
            ModelCardViewModel,
            SamplerCardViewModel,
            ModulesCardViewModel,
            SeedCardViewModel,
            BatchSizeCardViewModel
        );

        // When refiner is provided in model card, enable for sampler
        AddDisposable(
            ModelCardViewModel
                .WhenPropertyChanged(x => x.IsRefinerSelectionEnabled)
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(e =>
                {
                    SamplerCardViewModel.IsRefinerStepsEnabled =
                        e.Sender is { IsRefinerSelectionEnabled: true, SelectedRefiner: not null };
                })
        );
    }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        base.BuildPrompt(args);

        var builder = args.Builder;

        // Load constants
        builder.Connections.Seed = args.SeedOverride switch
        {
            { } seed => Convert.ToUInt64(seed),
            _ => Convert.ToUInt64(SeedCardViewModel.Seed)
        };

        var applyArgs = args.ToModuleApplyStepEventArgs();

        BatchSizeCardViewModel.ApplyStep(applyArgs);

        // Load models
        ModelCardViewModel.ApplyStep(applyArgs);

        var isUnetLoader = ModelCardViewModel.SelectedModelLoader is ModelLoader.Gguf or ModelLoader.Unet;
        var useSd3Latent =
            SamplerCardViewModel.ModulesCardViewModel.IsModuleEnabled<FluxGuidanceModule>() || isUnetLoader;

        if (useSd3Latent)
        {
            builder.SetupEmptySd3LatentSource(
                SamplerCardViewModel.Width,
                SamplerCardViewModel.Height,
                BatchSizeCardViewModel.BatchSize,
                BatchSizeCardViewModel.IsBatchIndexEnabled ? BatchSizeCardViewModel.BatchIndex : null
            );
        }
        else
        {
            // Setup empty latent
            builder.SetupEmptyLatentSource(
                SamplerCardViewModel.Width,
                SamplerCardViewModel.Height,
                BatchSizeCardViewModel.BatchSize,
                BatchSizeCardViewModel.IsBatchIndexEnabled ? BatchSizeCardViewModel.BatchIndex : null
            );
        }

        // Prompts and loras
        PromptCardViewModel.ApplyStep(applyArgs);

        // Setup Sampler and Refiner if enabled
        if (isUnetLoader)
        {
            SamplerCardViewModel.ApplyStepsInitialFluxSampler(applyArgs);
        }
        else
        {
            SamplerCardViewModel.ApplyStep(applyArgs);
        }

        // Hires fix if enabled
        foreach (var module in ModulesCardViewModel.Cards.OfType<ModuleBase>())
        {
            module.ApplyStep(applyArgs);
        }

        applyArgs.InvokeAllPreOutputActions();

        builder.SetupOutputImage();
    }

    /// <inheritdoc />
    protected override IEnumerable<ImageSource> GetInputImages()
    {
        var samplerImages = SamplerCardViewModel
            .ModulesCardViewModel.Cards.OfType<IInputImageProvider>()
            .SelectMany(m => m.GetInputImages());

        var moduleImages = ModulesCardViewModel
            .Cards.OfType<IInputImageProvider>()
            .SelectMany(m => m.GetInputImages());

        return samplerImages.Concat(moduleImages);
    }

    /// <inheritdoc />
    protected override async Task GenerateImageImpl(
        GenerateOverrides overrides,
        CancellationToken cancellationToken
    )
    {
        // Validate the prompts
        if (!await PromptCardViewModel.ValidatePrompts())
            return;

        if (!await ModelCardViewModel.ValidateModel())
            return;

        foreach (var module in ModulesCardViewModel.Cards.OfType<ModuleBase>())
        {
            if (!module.IsEnabled)
                continue;

            if (module is not IValidatableModule validatableModule)
                continue;

            if (!await validatableModule.Validate())
            {
                return;
            }
        }

        if (!await CheckClientConnectedWithPrompt() || !ClientManager.IsConnected)
            return;

        // If enabled, randomize the seed
        var seedCard = StackCardViewModel.GetCard<SeedCardViewModel>();
        if (overrides is not { UseCurrentSeed: true } && seedCard.IsRandomizeEnabled)
        {
            seedCard.GenerateNewSeed();
        }

        var batches = BatchSizeCardViewModel.BatchCount;

        var batchArgs = new List<ImageGenerationEventArgs>();

        for (var i = 0; i < batches; i++)
        {
            var seed = seedCard.Seed + i;

            var buildPromptArgs = new BuildPromptEventArgs { Overrides = overrides, SeedOverride = seed };
            BuildPrompt(buildPromptArgs);

            // update seed in project for batches
            var inferenceProject = InferenceProjectDocument.FromLoadable(this);
            if (inferenceProject.State?["Seed"]?["Seed"] is not null)
            {
                inferenceProject = inferenceProject.WithState(x => x["Seed"]["Seed"] = seed);
            }

            var generationArgs = new ImageGenerationEventArgs
            {
                Client = ClientManager.Client,
                Nodes = buildPromptArgs.Builder.ToNodeDictionary(),
                OutputNodeNames = buildPromptArgs.Builder.Connections.OutputNodeNames.ToArray(),
                Parameters = SaveStateToParameters(new GenerationParameters()) with
                {
                    Seed = Convert.ToUInt64(seed)
                },
                Project = inferenceProject,
                FilesToTransfer = buildPromptArgs.FilesToTransfer,
                BatchIndex = i,
                // Only clear output images on the first batch
                ClearOutputImages = i == 0
            };

            batchArgs.Add(generationArgs);
        }

        // Run batches
        foreach (var args in batchArgs)
        {
            await RunGeneration(args, cancellationToken);
        }
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        PromptCardViewModel.LoadStateFromParameters(parameters);
        SamplerCardViewModel.LoadStateFromParameters(parameters);
        ModelCardViewModel.LoadStateFromParameters(parameters);

        SeedCardViewModel.Seed = Convert.ToInt64(parameters.Seed);

        if (Math.Abs(SamplerCardViewModel.DenoiseStrength - 1.0d) > 0.01d)
        {
            SamplerCardViewModel.DenoiseStrength = 1.0d;
        }
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        parameters = PromptCardViewModel.SaveStateToParameters(parameters);
        parameters = SamplerCardViewModel.SaveStateToParameters(parameters);
        parameters = ModelCardViewModel.SaveStateToParameters(parameters);

        parameters.Seed = (ulong)SeedCardViewModel.Seed;

        return parameters;
    }

    // Deserialization overrides
    public override void LoadStateFromJsonObject(JsonObject state, int version)
    {
        // For v2 and below, do migration
        if (version <= 2)
        {
            ModulesCardViewModel.Clear();

            // Add by default the original cards as steps - HiresFix, Upscaler
            ModulesCardViewModel.AddModule<HiresFixModule>(module =>
            {
                module.IsEnabled = state.GetPropertyValueOrDefault<bool>("IsHiresFixEnabled");

                if (state.TryGetPropertyValue("HiresSampler", out var hiresSamplerState))
                {
                    module
                        .GetCard<SamplerCardViewModel>()
                        .LoadStateFromJsonObject(hiresSamplerState!.AsObject());
                }

                if (state.TryGetPropertyValue("HiresUpscaler", out var hiresUpscalerState))
                {
                    module
                        .GetCard<UpscalerCardViewModel>()
                        .LoadStateFromJsonObject(hiresUpscalerState!.AsObject());
                }
            });

            ModulesCardViewModel.AddModule<UpscalerModule>(module =>
            {
                module.IsEnabled = state.GetPropertyValueOrDefault<bool>("IsUpscaleEnabled");

                if (state.TryGetPropertyValue("Upscaler", out var upscalerState))
                {
                    module
                        .GetCard<UpscalerCardViewModel>()
                        .LoadStateFromJsonObject(upscalerState!.AsObject());
                }
            });

            // Add FreeU to sampler
            SamplerCardViewModel.ModulesCardViewModel.AddModule<FreeUModule>(module =>
            {
                module.IsEnabled = state.GetPropertyValueOrDefault<bool>("IsFreeUEnabled");
            });
        }

        base.LoadStateFromJsonObject(state);
    }

    public override void OnUnloaded()
    {
        base.OnUnloaded();
        ModelCardViewModel.OnUnloaded();
        StackCardViewModel.OnUnloaded();
        ModulesCardViewModel.OnUnloaded();
        SamplerCardViewModel.OnUnloaded();
        PromptCardViewModel.OnUnloaded();
        BatchSizeCardViewModel.OnUnloaded();
        SeedCardViewModel.OnUnloaded();
    }
}
