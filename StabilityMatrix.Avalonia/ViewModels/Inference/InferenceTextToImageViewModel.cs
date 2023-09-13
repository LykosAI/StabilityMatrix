using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using NLog;
using Refit;
using SkiaSharp;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;
using InferenceTextToImageView = StabilityMatrix.Avalonia.Views.Inference.InferenceTextToImageView;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceTextToImageView), persistent: true)]
public partial class InferenceTextToImageViewModel : InferenceTabViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly IModelIndexService modelIndexService;
    private readonly IImageIndexService imageIndexService;

    [JsonIgnore]
    public IInferenceClientManager ClientManager { get; }

    [JsonIgnore]
    public StackCardViewModel StackCardViewModel { get; }

    [JsonPropertyName("Model")]
    public ModelCardViewModel ModelCardViewModel { get; }

    [JsonPropertyName("Sampler")]
    public SamplerCardViewModel SamplerCardViewModel { get; }

    [JsonPropertyName("ImageGallery")]
    public ImageGalleryCardViewModel ImageGalleryCardViewModel { get; }

    [JsonPropertyName("ImageFolder")]
    public ImageFolderCardViewModel ImageFolderCardViewModel { get; }

    [JsonPropertyName("Prompt")]
    public PromptCardViewModel PromptCardViewModel { get; }

    [JsonPropertyName("Upscaler")]
    public UpscalerCardViewModel UpscalerCardViewModel { get; }

    [JsonPropertyName("HiresSampler")]
    public SamplerCardViewModel HiresSamplerCardViewModel { get; }

    [JsonPropertyName("HiresUpscaler")]
    public UpscalerCardViewModel HiresUpscalerCardViewModel { get; }

    [JsonPropertyName("BatchSize")]
    public BatchSizeCardViewModel BatchSizeCardViewModel { get; }

    [JsonPropertyName("Seed")]
    public SeedCardViewModel SeedCardViewModel { get; }

    public bool IsHiresFixEnabled
    {
        get => StackCardViewModel.GetCard<StackExpanderViewModel>().IsEnabled;
        set => StackCardViewModel.GetCard<StackExpanderViewModel>().IsEnabled = value;
    }

    public bool IsUpscaleEnabled
    {
        get => StackCardViewModel.GetCard<StackExpanderViewModel>(1).IsEnabled;
        set => StackCardViewModel.GetCard<StackExpanderViewModel>(1).IsEnabled = value;
    }

    [JsonIgnore]
    public ProgressViewModel OutputProgress { get; } = new();

    [ObservableProperty]
    [property: JsonIgnore]
    private string? outputImageSource;

    public InferenceTextToImageViewModel(
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        ServiceManager<ViewModelBase> vmFactory,
        IModelIndexService modelIndexService,
        IImageIndexService imageIndexService
    )
    {
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;
        this.modelIndexService = modelIndexService;
        this.imageIndexService = imageIndexService;
        ClientManager = inferenceClientManager;

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
        });

        ImageGalleryCardViewModel = vmFactory.Get<ImageGalleryCardViewModel>();
        ImageFolderCardViewModel = vmFactory.Get<ImageFolderCardViewModel>();
        PromptCardViewModel = vmFactory.Get<PromptCardViewModel>();
        HiresSamplerCardViewModel = vmFactory.Get<SamplerCardViewModel>(samplerCard =>
        {
            samplerCard.IsDenoiseStrengthEnabled = true;
        });
        HiresUpscalerCardViewModel = vmFactory.Get<UpscalerCardViewModel>();
        UpscalerCardViewModel = vmFactory.Get<UpscalerCardViewModel>();
        BatchSizeCardViewModel = vmFactory.Get<BatchSizeCardViewModel>();

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();

        StackCardViewModel.AddCards(
            new LoadableViewModelBase[]
            {
                ModelCardViewModel,
                SamplerCardViewModel,
                // Hires Fix
                vmFactory.Get<StackExpanderViewModel>(stackExpander =>
                {
                    stackExpander.Title = "Hires Fix";
                    stackExpander.AddCards(
                        new LoadableViewModelBase[]
                        {
                            HiresUpscalerCardViewModel,
                            HiresSamplerCardViewModel
                        }
                    );
                }),
                vmFactory.Get<StackExpanderViewModel>(stackExpander =>
                {
                    stackExpander.Title = "Upscale";
                    stackExpander.AddCards(new LoadableViewModelBase[] { UpscalerCardViewModel });
                }),
                SeedCardViewModel,
                BatchSizeCardViewModel,
            }
        );

        // When refiner is provided in model card, enable for sampler
        ModelCardViewModel
            .WhenPropertyChanged(x => x.IsRefinerSelectionEnabled)
            .Subscribe(e =>
            {
                SamplerCardViewModel.IsRefinerStepsEnabled =
                    e.Sender is { IsRefinerSelectionEnabled: true, SelectedRefiner: not null };
            });

        GenerateImageCommand.WithConditionalNotificationErrorHandler(notificationService);
    }

    private (NodeDictionary prompt, string[] outputs) BuildPrompt(
        GenerateOverrides? overrides = null
    )
    {
        using var _ = new CodeTimer();

        var builder = new ComfyNodeBuilder();
        var nodes = builder.Nodes;

        // Setup empty latent
        builder.SetupLatentSource(BatchSizeCardViewModel, SamplerCardViewModel);

        // Setup base stage
        builder.SetupBaseSampler(
            SeedCardViewModel,
            SamplerCardViewModel,
            PromptCardViewModel,
            ModelCardViewModel,
            modelIndexService
        );

        // Setup refiner stage if enabled
        if (
            ModelCardViewModel is
            { IsRefinerSelectionEnabled: true, SelectedRefiner.IsDefault: false }
        )
        {
            builder.SetupRefinerSampler(
                SeedCardViewModel,
                SamplerCardViewModel,
                PromptCardViewModel,
                ModelCardViewModel,
                modelIndexService
            );
        }

        // Override with custom VAE if enabled
        if (ModelCardViewModel is { IsVaeSelectionEnabled: true, SelectedVae.IsDefault: false })
        {
            builder.Connections.BaseVAE = nodes
                .AddNamedNode(
                    ComfyNodeBuilder.VAELoader("VAELoader", ModelCardViewModel.SelectedVae.FileName)
                )
                .Output;
        }

        // If hi-res fix is enabled, add the LatentUpscale node and another KSampler node
        if (overrides?.IsHiresFixEnabled ?? IsHiresFixEnabled)
        {
            // Requested upscale to this size
            var hiresSize = builder.Connections.GetScaledLatentSize(
                HiresUpscalerCardViewModel.Scale
            );

            LatentNodeConnection hiresLatent;

            // Select between latent upscale and normal upscale based on the upscale method
            var selectedUpscaler = HiresUpscalerCardViewModel.SelectedUpscaler!.Value;

            if (selectedUpscaler.Type == ComfyUpscalerType.None)
            {
                // If no upscaler selected or none, just use the latent image
                hiresLatent = builder.Connections.Latent!;
            }
            else
            {
                // Otherwise upscale the latent image
                hiresLatent = builder
                    .Group_UpscaleToLatent(
                        "HiresFix",
                        builder.Connections.Latent!,
                        builder.Connections.GetRefinerOrBaseVAE(),
                        selectedUpscaler,
                        hiresSize.Width,
                        hiresSize.Height
                    )
                    .Output;
            }

            // Use refiner model if set, or base if not
            var hiresSampler = nodes.AddNamedNode(
                ComfyNodeBuilder.KSampler(
                    "HiresSampler",
                    builder.Connections.GetRefinerOrBaseModel(),
                    Convert.ToUInt64(SeedCardViewModel.Seed),
                    HiresSamplerCardViewModel.Steps,
                    HiresSamplerCardViewModel.CfgScale,
                    // Use hires sampler name if not null, otherwise use the normal sampler
                    HiresSamplerCardViewModel.SelectedSampler
                        ?? SamplerCardViewModel.SelectedSampler
                        ?? throw new ValidationException("Sampler not selected"),
                    HiresSamplerCardViewModel.SelectedScheduler
                        ?? SamplerCardViewModel.SelectedScheduler
                        ?? throw new ValidationException("Scheduler not selected"),
                    builder.Connections.GetRefinerOrBaseConditioning(),
                    builder.Connections.GetRefinerOrBaseNegativeConditioning(),
                    hiresLatent,
                    HiresSamplerCardViewModel.DenoiseStrength
                )
            );

            // Set as latest latent
            builder.Connections.Latent = hiresSampler.Output;
            builder.Connections.LatentSize = hiresSize;
        }

        // If upscale is enabled, add another upscale group
        if (IsUpscaleEnabled)
        {
            var upscaleSize = builder.Connections.GetScaledLatentSize(UpscalerCardViewModel.Scale);

            // Build group
            var postUpscaleGroup = builder.Group_UpscaleToImage(
                "PostUpscale",
                builder.Connections.Latent!,
                builder.Connections.GetRefinerOrBaseVAE(),
                UpscalerCardViewModel.SelectedUpscaler!.Value,
                upscaleSize.Width,
                upscaleSize.Height
            );

            // Set as the image output
            builder.Connections.Image = postUpscaleGroup.Output;
        }

        // Output
        var outputName = builder.SetupOutputImage();

        return (builder.ToNodeDictionary(), new[] { outputName });
    }

    private void OnProgressUpdateReceived(object? sender, ComfyProgressUpdateEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            OutputProgress.Value = args.Value;
            OutputProgress.Maximum = args.Maximum;
            OutputProgress.IsIndeterminate = false;

            OutputProgress.Text =
                $"({args.Value} / {args.Maximum})"
                + (args.RunningNode != null ? $" {args.RunningNode}" : "");
        });
    }

    private void OnPreviewImageReceived(object? sender, ComfyWebSocketImageData args)
    {
        ImageGalleryCardViewModel.SetPreviewImage(args.ImageBytes);
    }

    private async Task GenerateImageImpl(
        GenerateOverrides? overrides = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate the prompts
        if (!await PromptCardViewModel.ValidatePrompts())
        {
            return;
        }

        if (!ClientManager.IsConnected)
        {
            notificationService.Show("Client not connected", "Please connect first");
            return;
        }

        // If enabled, randomize the seed
        var seedCard = StackCardViewModel.GetCard<SeedCardViewModel>();
        if (overrides is not { UseCurrentSeed: true } && seedCard.IsRandomizeEnabled)
        {
            seedCard.GenerateNewSeed();
        }

        var client = ClientManager.Client;

        var (nodes, outputNodeNames) = BuildPrompt(overrides);

        var generationInfo = new GenerationParameters
        {
            Seed = (ulong)seedCard.Seed,
            Steps = SamplerCardViewModel.Steps,
            CfgScale = SamplerCardViewModel.CfgScale,
            Sampler = SamplerCardViewModel.SelectedSampler?.Name,
            ModelName = ModelCardViewModel.SelectedModelName,
            // TODO: ModelHash
            PositivePrompt = PromptCardViewModel.PromptDocument.Text,
            NegativePrompt = PromptCardViewModel.NegativePromptDocument.Text
        };
        var smproj = InferenceProjectDocument.FromLoadable(this);

        // Connect preview image handler
        client.PreviewImageReceived += OnPreviewImageReceived;

        ComfyTask? promptTask = null;
        try
        {
            // Register to interrupt if user cancels
            cancellationToken.Register(() =>
            {
                Logger.Info("Cancelling prompt");
                client
                    .InterruptPromptAsync(new CancellationTokenSource(5000).Token)
                    .SafeFireAndForget();
            });

            try
            {
                promptTask = await client.QueuePromptAsync(nodes, cancellationToken);
            }
            catch (ApiException e)
            {
                Logger.Warn(e, "Api exception while queuing prompt");
                await DialogHelper.CreateApiExceptionDialog(e, "Api Error").ShowAsync();
                return;
            }

            // Register progress handler
            promptTask.ProgressUpdate += OnProgressUpdateReceived;

            // Wait for prompt to finish
            await promptTask.Task.WaitAsync(cancellationToken);
            Logger.Trace($"Prompt task {promptTask.Id} finished");

            // Get output images
            var imageOutputs = await client.GetImagesForExecutedPromptAsync(
                promptTask.Id,
                cancellationToken
            );

            ImageGalleryCardViewModel.ImageSources.Clear();

            if (!imageOutputs.TryGetValue(outputNodeNames[0], out var images) || images is null)
            {
                // No images match
                notificationService.Show("No output", "Did not receive any output images");
                return;
            }
        }
        finally
        {
            // Disconnect progress handler
            OutputProgress.Value = 0;
            OutputProgress.Text = "";
            ImageGalleryCardViewModel.PreviewImage?.Dispose();
            ImageGalleryCardViewModel.PreviewImage = null;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = false;

            promptTask?.Dispose();
            client.PreviewImageReceived -= OnPreviewImageReceived;
        }
    }

    private async Task ProcessOutputs(IReadOnlyList<ComfyImage> images)
    {
        List<ImageSource> outputImages;
        // Use local file path if available, otherwise use remote URL
        if (client.OutputImagesDir is { } outputPath)
        {
            outputImages = new List<ImageSource>();
            foreach (var image in images)
            {
                var filePath = image.ToFilePath(outputPath);

                var bytesWithMetadata = PngDataHelper.AddMetadata(
                    await filePath.ReadAllBytesAsync(),
                    generationInfo,
                    smproj
                );

                /*await using (var readStream = filePath.Info.OpenWrite())
                {
                    using (var reader = new BinaryReader(readStream))
                    {

                    }
                }*/

                await using (var outputStream = filePath.Info.OpenWrite())
                {
                    await outputStream.WriteAsync(bytesWithMetadata);
                    await outputStream.FlushAsync();
                }

                outputImages.Add(new ImageSource(filePath));

                imageIndexService.OnImageAdded(filePath);
            }
        }
        else
        {
            outputImages = images!
                .Select(i => new ImageSource(i.ToUri(client.BaseAddress)))
                .ToList();
        }

        // Download all images to make grid, if multiple
        if (outputImages.Count > 1)
        {
            var loadedImages = outputImages
                .Select(i => SKImage.FromEncodedData(i.LocalFile?.Info.OpenRead()))
                .ToImmutableArray();

            var grid = ImageProcessor.CreateImageGrid(loadedImages);
            var gridBytes = grid.Encode().ToArray();
            var gridBytesWithMetadata = PngDataHelper.AddMetadata(
                gridBytes,
                generationInfo,
                smproj
            );

            // Save to disk
            var lastName = outputImages.Last().LocalFile?.Info.Name;
            var gridPath = client.OutputImagesDir!.JoinFile($"grid-{lastName}");

            await using (var fileStream = gridPath.Info.OpenWrite())
            {
                await fileStream.WriteAsync(gridBytesWithMetadata, cancellationToken);
            }

            // Insert to start of images
            var gridImage = new ImageSource(gridPath);
            // Preload
            await gridImage.GetBitmapAsync();
            ImageGalleryCardViewModel.ImageSources.Add(gridImage);

            imageIndexService.OnImageAdded(gridPath);
        }

        // Add rest of images
        foreach (var img in outputImages)
        {
            // Preload
            await img.GetBitmapAsync();
            ImageGalleryCardViewModel.ImageSources.Add(img);
        }
    }

    [RelayCommand(IncludeCancelCommand = true, FlowExceptionsToTaskScheduler = true)]
    private async Task GenerateImage(
        string? options = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var overrides = new GenerateOverrides
            {
                IsHiresFixEnabled = options?.Contains("hires_fix"),
                UseCurrentSeed = options?.Contains("current_seed")
            };

            await GenerateImageImpl(overrides, cancellationToken);
        }
        catch (OperationCanceledException e)
        {
            Logger.Debug($"[Image Generation Canceled] {e.Message}");
        }
    }

    internal class GenerateOverrides
    {
        public bool? IsHiresFixEnabled { get; set; }
        public bool? UseCurrentSeed { get; set; }
    }
}
