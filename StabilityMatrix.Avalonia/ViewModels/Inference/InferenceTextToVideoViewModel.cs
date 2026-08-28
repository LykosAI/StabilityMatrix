using System.Text.Json.Serialization;
using DesktopNotifications;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Video;
using StabilityMatrix.Avalonia.Views.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

/// <summary>
/// The generic text-to-video tab. Persisted as <see cref="InferenceProjectType.WanTextToVideo"/>
/// (a frozen wire name — see the enum docs).
/// </summary>
[View(typeof(InferenceTextToVideoView), IsPersistent = true)]
[RegisterScoped<InferenceTextToVideoViewModel>, ManagedService]
public class InferenceTextToVideoViewModel : InferenceGenerationViewModelBase, IParametersLoadableState
{
    private readonly INotificationService notificationService;

    [JsonIgnore]
    public StackCardViewModel StackCardViewModel { get; }

    [JsonPropertyName("Model")]
    public VideoModelCardViewModel ModelCardViewModel { get; }

    [JsonPropertyName("Sampler")]
    public SamplerCardViewModel SamplerCardViewModel { get; }

    [JsonPropertyName("BatchSize")]
    public BatchSizeCardViewModel BatchSizeCardViewModel { get; }

    [JsonPropertyName("Seed")]
    public SeedCardViewModel SeedCardViewModel { get; }

    [JsonPropertyName("Prompt")]
    public PromptCardViewModel PromptCardViewModel { get; }

    [JsonPropertyName("VideoOutput")]
    public VideoOutputSettingsCardViewModel VideoOutputSettingsCardViewModel { get; }

    public InferenceTextToVideoViewModel(
        IServiceManager<ViewModelBase> vmFactory,
        IInferenceClientManager inferenceClientManager,
        INotificationService notificationService,
        ISettingsManager settingsManager,
        RunningPackageService runningPackageService,
        IPromptQueueService promptQueueService
    )
        : base(
            vmFactory,
            inferenceClientManager,
            notificationService,
            settingsManager,
            runningPackageService,
            promptQueueService
        )
    {
        this.notificationService = notificationService;
        SeedCardViewModel = vmFactory.Get<SeedCardViewModel>();
        SeedCardViewModel.GenerateNewSeed();

        ModelCardViewModel = vmFactory.Get<VideoModelCardViewModel>();
        ModelCardViewModel.RecommendedDefaultsRequested += ApplyRecommendedDefaults;

        SamplerCardViewModel = vmFactory.Get<VideoSamplerCardViewModel>(samplerCard =>
        {
            samplerCard.IsDimensionsEnabled = true;
            samplerCard.IsCfgScaleEnabled = true;
            samplerCard.IsSamplerSelectionEnabled = true;
            samplerCard.IsSchedulerSelectionEnabled = true;
            samplerCard.DenoiseStrength = 1.0d;
            samplerCard.EnableAddons = true;
            samplerCard.IsLengthEnabled = true;
            samplerCard.Width = 832;
            samplerCard.Height = 480;
            samplerCard.Length = 33;
        });

        PromptCardViewModel = AddDisposable(vmFactory.Get<PromptCardViewModel>());

        BatchSizeCardViewModel = vmFactory.Get<BatchSizeCardViewModel>();

        VideoOutputSettingsCardViewModel = vmFactory.Get<VideoOutputSettingsCardViewModel>(vm =>
            vm.Fps = 16.0d
        );

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();
        StackCardViewModel.AddCards(
            ModelCardViewModel,
            SamplerCardViewModel,
            SeedCardViewModel,
            BatchSizeCardViewModel,
            VideoOutputSettingsCardViewModel
        );
    }

    /// <summary>
    /// Applies the recommended Wan settings when the model card's ✨ button is clicked. The KSampler
    /// values match the native ComfyUI templates for both variants; only the resolution, frame count and
    /// output fps differ between the 5B TI2V model (720p / 24 fps) and the 14B / 2.1 models (480p / 16 fps).
    /// </summary>
    private void ApplyRecommendedDefaults(bool isWan22FiveB)
    {
        SamplerCardViewModel.SelectedSampler = ComfySampler.UniPC;
        SamplerCardViewModel.SelectedScheduler = ComfyScheduler.Simple;
        SamplerCardViewModel.Steps = 20;
        SamplerCardViewModel.CfgScale = 5.0d;
        ModelCardViewModel.Shift = 8.0d;

        if (isWan22FiveB)
        {
            SamplerCardViewModel.Width = 1280;
            SamplerCardViewModel.Height = 704;
            SamplerCardViewModel.Length = 121;
            VideoOutputSettingsCardViewModel.Fps = 24.0d;
        }
        else
        {
            SamplerCardViewModel.Width = 832;
            SamplerCardViewModel.Height = 480;
            SamplerCardViewModel.Length = 81;
            VideoOutputSettingsCardViewModel.Fps = 16.0d;
        }
    }

    /// <inheritdoc />
    protected override void BuildPrompt(BuildPromptEventArgs args)
    {
        base.BuildPrompt(args);

        var applyArgs = args.ToModuleApplyStepEventArgs();
        var builder = args.Builder;

        builder.Connections.Seed = args.SeedOverride switch
        {
            { } seed => Convert.ToUInt64(seed),
            _ => Convert.ToUInt64(SeedCardViewModel.Seed),
        };

        // Load models (also wires the VAE used by the Wan 2.2 5B latent below)
        ModelCardViewModel.ApplyStep(applyArgs);

        // The 5B TI2V model uses a different VAE (48-channel / 16x) than the 14B / Wan 2.1 models,
        // so its empty latent must come from Wan22ImageToVideoLatent instead of EmptyHunyuanLatentVideo.
        builder.SetupEmptyLatentSource(
            SamplerCardViewModel.Width,
            SamplerCardViewModel.Height,
            BatchSizeCardViewModel.BatchSize,
            BatchSizeCardViewModel.IsBatchIndexEnabled ? BatchSizeCardViewModel.BatchIndex : null,
            SamplerCardViewModel.Length,
            ModelCardViewModel.UseWan22FiveBLatent ? LatentType.Wan22 : LatentType.Hunyuan
        );

        BatchSizeCardViewModel.ApplyStep(applyArgs);

        PromptCardViewModel.ApplyStep(applyArgs);

        SamplerCardViewModel.ApplyStep(applyArgs);

        applyArgs.InvokeAllPreOutputActions();

        // Animated webp output
        VideoOutputSettingsCardViewModel.ApplyStep(applyArgs);
    }

    /// <inheritdoc />
    protected override async Task GenerateImageImpl(
        GenerateOverrides overrides,
        CancellationToken cancellationToken
    )
    {
        if (!await CheckClientConnectedWithPrompt(cancellationToken) || !ClientManager.IsConnected)
        {
            return;
        }

        if (!await ModelCardViewModel.ValidateModel())
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
                    Seed = Convert.ToUInt64(seed),
                },
                Project = inferenceProject,
                FilesToTransfer = buildPromptArgs.FilesToTransfer,
                BatchIndex = i,
                // Only clear output images on the first batch
                ClearOutputImages = i == 0,
            };

            batchArgs.Add(generationArgs);
        }

        // Run batches
        foreach (var args in batchArgs)
        {
            await RunGeneration(args, cancellationToken);
        }

        // Only show batch notification when there's more than one item
        // (single items already get a "Prompt Completed" notification)
        if (batches > 1)
        {
            await notificationService.ShowAsync(
                NotificationKey.Inference_BatchCompleted,
                new Notification
                {
                    Title = "Batch Completed",
                    Body =
                        $"Batch of {batches} items [{Guid.NewGuid().ToString()[..7].ToLower()}] completed successfully",
                    BodyImagePath = ImageGalleryCardViewModel
                        .ImageSources.LastOrDefault()
                        ?.LocalFile?.FullPath,
                }
            );
        }
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        SamplerCardViewModel.LoadStateFromParameters(parameters);
        ModelCardViewModel.LoadStateFromParameters(parameters);
        PromptCardViewModel.LoadStateFromParameters(parameters);
        VideoOutputSettingsCardViewModel.LoadStateFromParameters(parameters);
        SeedCardViewModel.Seed = Convert.ToInt64(parameters.Seed);
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        parameters = SamplerCardViewModel.SaveStateToParameters(parameters);
        parameters = ModelCardViewModel.SaveStateToParameters(parameters);
        parameters = PromptCardViewModel.SaveStateToParameters(parameters);
        parameters = VideoOutputSettingsCardViewModel.SaveStateToParameters(parameters);

        parameters.Seed = (ulong)SeedCardViewModel.Seed;

        return parameters;
    }
}
