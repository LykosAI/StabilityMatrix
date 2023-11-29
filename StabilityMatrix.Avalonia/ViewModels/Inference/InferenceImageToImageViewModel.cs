using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceImageToImageView), IsPersistent = true)]
[Transient, ManagedService]
public partial class InferenceImageToImageViewModel
    : InferenceGenerationViewModelBase,
        IParametersLoadableState
{
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

    [JsonPropertyName("SelectImage")]
    public SelectImageCardViewModel SelectImageCardViewModel { get; }

    /// <inheritdoc />
    public InferenceImageToImageViewModel(
        ServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager inferenceClientManager,
        INotificationService notificationService,
        ISettingsManager settingsManager
    )
        : base(vmFactory, inferenceClientManager, notificationService, settingsManager)
    {
        SeedCardViewModel = vmFactory.Get<SeedCardViewModel>();
        SeedCardViewModel.GenerateNewSeed();

        ModelCardViewModel = vmFactory.Get<ModelCardViewModel>();

        SamplerCardViewModel = vmFactory.Get<SamplerCardViewModel>(samplerCard =>
        {
            samplerCard.IsDimensionsEnabled = true;
            samplerCard.IsCfgScaleEnabled = true;
            samplerCard.IsSamplerSelectionEnabled = true;
            samplerCard.IsSchedulerSelectionEnabled = true;
            samplerCard.IsDenoiseStrengthEnabled = true;
        });

        PromptCardViewModel = vmFactory.Get<PromptCardViewModel>();
        BatchSizeCardViewModel = vmFactory.Get<BatchSizeCardViewModel>();

        ModulesCardViewModel = vmFactory.Get<StackEditableCardViewModel>(modulesCard =>
        {
            modulesCard.AvailableModules = new[]
            {
                typeof(HiresFixModule),
                typeof(UpscalerModule),
                typeof(SaveImageModule)
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

        SelectImageCardViewModel = vmFactory.Get<SelectImageCardViewModel>();

        // When refiner is provided in model card, enable for sampler
        ModelCardViewModel
            .WhenPropertyChanged(x => x.IsRefinerSelectionEnabled)
            .Subscribe(e =>
            {
                SamplerCardViewModel.IsRefinerStepsEnabled =
                    e.Sender is { IsRefinerSelectionEnabled: true, SelectedRefiner: not null };
            });
    }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        base.BuildPrompt(args);

        var builder = args.Builder;

        builder.Connections.Seed = args.SeedOverride switch
        {
            { } seed => Convert.ToUInt64(seed),
            _ => Convert.ToUInt64(SeedCardViewModel.Seed)
        };

        // Load models
        ModelCardViewModel.ApplyStep(args);

        // Setup image latent
        builder.SetupImageLatentSource(SelectImageCardViewModel, BatchSizeCardViewModel.BatchIndex);

        // Prompts and loras
        PromptCardViewModel.ApplyStep(args);

        // Setup Sampler and Refiner if enabled
        SamplerCardViewModel.ApplyStep(args);

        // Hires fix if enabled
        foreach (var module in ModulesCardViewModel.Cards.OfType<ModuleBase>())
        {
            module.ApplyStep(args);
        }

        builder.SetupOutputImage();
    }

    /// <inheritdoc />
    protected override IEnumerable<ImageSource> GetInputImages()
    {
        if (SelectImageCardViewModel.ImageSource is { } imageSource)
        {
            yield return imageSource;
        }
    }

    /// <inheritdoc />
    protected override async Task GenerateImageImpl(
        GenerateOverrides overrides,
        CancellationToken cancellationToken
    )
    {
        // Validate the prompts
        if (!await PromptCardViewModel.ValidatePrompts())
        {
            return;
        }

        if (!await CheckClientConnectedWithPrompt() || !ClientManager.IsConnected)
        {
            return;
        }

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

            var buildPromptArgs = new BuildPromptEventArgs
            {
                Overrides = overrides,
                SeedOverride = seed
            };
            BuildPrompt(buildPromptArgs);

            var generationArgs = new ImageGenerationEventArgs
            {
                Client = ClientManager.Client,
                Nodes = buildPromptArgs.Builder.ToNodeDictionary(),
                OutputNodeNames = buildPromptArgs.Builder.Connections.OutputNodeNames.ToArray(),
                Parameters = SaveStateToParameters(new GenerationParameters()),
                Project = InferenceProjectDocument.FromLoadable(this),
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
}
