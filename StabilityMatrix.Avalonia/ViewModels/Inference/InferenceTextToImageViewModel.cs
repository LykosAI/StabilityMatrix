using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using Refit;
using SkiaSharp;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceTextToImageView), persistent: true)]
public partial class InferenceTextToImageViewModel : InferenceTabViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    public IInferenceClientManager ClientManager { get; }

    public ImageGalleryCardViewModel ImageGalleryCardViewModel { get; }
    public PromptCardViewModel PromptCardViewModel { get; }
    public StackCardViewModel StackCardViewModel { get; }

    public UpscalerCardViewModel UpscalerCardViewModel =>
        StackCardViewModel.GetCard<StackExpanderViewModel>().GetCard<UpscalerCardViewModel>();

    public SamplerCardViewModel HiresSamplerCardViewModel =>
        StackCardViewModel.GetCard<StackExpanderViewModel>().GetCard<SamplerCardViewModel>();

    public bool IsHiresFixEnabled => StackCardViewModel.GetCard<StackExpanderViewModel>().IsEnabled;

    public bool IsUpscaleEnabled => StackCardViewModel.GetCard<StackExpanderViewModel>(1).IsEnabled;
    
    [JsonIgnore]
    public ProgressViewModel OutputProgress { get; } = new();

    [ObservableProperty]
    [property: JsonIgnore]
    private string? outputImageSource;

    public InferenceTextToImageViewModel(
        INotificationService notificationService,
        IInferenceClientManager inferenceClientManager,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;
        ClientManager = inferenceClientManager;

        // Get sub view models from service manager

        var seedCard = vmFactory.Get<SeedCardViewModel>();
        seedCard.GenerateNewSeed();

        ImageGalleryCardViewModel = vmFactory.Get<ImageGalleryCardViewModel>();
        PromptCardViewModel = vmFactory.Get<PromptCardViewModel>();

        StackCardViewModel = vmFactory.Get<StackCardViewModel>();

        StackCardViewModel.AddCards(
            new LoadableViewModelBase[]
            {
                // Model Card
                vmFactory.Get<ModelCardViewModel>(),
                // Sampler
                vmFactory.Get<SamplerCardViewModel>(),
                // Hires Fix
                vmFactory.Get<StackExpanderViewModel>(stackExpander =>
                {
                    stackExpander.Title = "Hires Fix";
                    stackExpander.AddCards(
                        new LoadableViewModelBase[]
                        {
                            // Hires Fix Upscaler
                            vmFactory.Get<UpscalerCardViewModel>(),
                            // Hires Fix Sampler
                            vmFactory.Get<SamplerCardViewModel>(samplerCard =>
                            {
                                samplerCard.IsDimensionsEnabled = false;
                                samplerCard.IsCfgScaleEnabled = false;
                                samplerCard.IsSamplerSelectionEnabled = false;
                                samplerCard.IsDenoiseStrengthEnabled = true;
                            })
                        }
                    );
                }),
                vmFactory.Get<StackExpanderViewModel>(stackExpander =>
                {
                    stackExpander.Title = "Upscale";
                    stackExpander.AddCards(
                        new LoadableViewModelBase[]
                        {
                            // Post processing upscaler
                            vmFactory.Get<UpscalerCardViewModel>(),
                        });
                }),
                // Seed
                seedCard,
                // Batch Size
                vmFactory.Get<BatchSizeCardViewModel>(),
            }
        );

        // GenerateImageCommand.WithNotificationErrorHandler(notificationService);
    }

    private (NodeDictionary prompt, string[] outputs) BuildPrompt(GenerateOverrides? overrides = null)
    {
        using var _ = new CodeTimer();

        var samplerCard = StackCardViewModel.GetCard<SamplerCardViewModel>();
        var batchCard = StackCardViewModel.GetCard<BatchSizeCardViewModel>();
        var modelCard = StackCardViewModel.GetCard<ModelCardViewModel>();
        var seedCard = StackCardViewModel.GetCard<SeedCardViewModel>();

        var prompt = new NodeDictionary();
        var builder = new ComfyNodeBuilder(prompt);

        var checkpointLoader = prompt.AddNamedNode(
            new NamedComfyNode("CheckpointLoader")
            {
                ClassType = "CheckpointLoaderSimple",
                Inputs = new Dictionary<string, object?>
                {
                    ["ckpt_name"] = modelCard.SelectedModelName
                }
            }
        );

        // Either use checkpoint VAE or custom VAE
        VAENodeConnection vaeSource;
        
        if (modelCard is {IsVaeSelectionEnabled: true, SelectedVae.IsDefault: false})
        {
            // Use custom VAE
            
            // Add a loader
            var vaeLoader = 
                prompt.AddNamedNode(ComfyNodeBuilder.VAELoader("VAELoader", modelCard.SelectedVae.FileName));
            
            // Set as source
            vaeSource = vaeLoader.Output;
        }
        else
        {
            // Use checkpoint VAE
            vaeSource = checkpointLoader.GetOutput<VAENodeConnection>(2);
        }

        var emptyLatentImage = prompt.AddNamedNode(
            new NamedComfyNode("EmptyLatentImage")
            {
                ClassType = "EmptyLatentImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["batch_size"] = batchCard.BatchSize,
                    ["height"] = samplerCard.Height,
                    ["width"] = samplerCard.Width,
                }
            }
        );

        var positiveClip = prompt.AddNamedNode(
            new NamedComfyNode("PositiveCLIP")
            {
                ClassType = "CLIPTextEncode",
                Inputs = new Dictionary<string, object?>
                {
                    ["clip"] = checkpointLoader.GetOutput(1),
                    ["text"] = PromptCardViewModel.PromptDocument.Text,
                }
            }
        );

        var negativeClip = prompt.AddNamedNode(
            new NamedComfyNode("NegativeCLIP")
            {
                ClassType = "CLIPTextEncode",
                Inputs = new Dictionary<string, object?>
                {
                    ["clip"] = checkpointLoader.GetOutput(1),
                    ["text"] = PromptCardViewModel.NegativePromptDocument.Text,
                }
            }
        );

        var sampler = prompt.AddNamedNode(
            ComfyNodeBuilder.KSampler(
                "Sampler",
                checkpointLoader.GetOutput<ModelNodeConnection>(0),
                Convert.ToUInt64(seedCard.Seed),
                samplerCard.Steps,
                samplerCard.CfgScale,
                samplerCard.SelectedSampler?.Name
                    ?? throw new InvalidOperationException("Sampler not selected"),
                "normal",
                positiveClip.GetOutput<ConditioningNodeConnection>(0),
                negativeClip.GetOutput<ConditioningNodeConnection>(0),
                emptyLatentImage.GetOutput<LatentNodeConnection>(0),
                samplerCard.DenoiseStrength
            )
        );

        var lastLatent = sampler.Output;
        var lastLatentWidth = samplerCard.Width;
        var lastLatentHeight = samplerCard.Height;
        
        var vaeDecoder = prompt.AddNamedNode(
            new NamedComfyNode("VAEDecoder")
            {
                ClassType = "VAEDecode",
                Inputs = new Dictionary<string, object?>
                {
                    ["samples"] = lastLatent,
                    ["vae"] = vaeSource
                }
            }
        );

        var saveImage = prompt.AddNamedNode(
            new NamedComfyNode("SaveImage")
            {
                ClassType = "SaveImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["filename_prefix"] = "SM-Inference",
                    ["images"] = vaeDecoder.GetOutput(0)
                }
            }
        );
        
        // If hi-res fix is enabled, add the LatentUpscale node and another KSampler node
        if (overrides is { IsHiresFixEnabled: true } || IsHiresFixEnabled)
        {
            var hiresUpscalerCard = UpscalerCardViewModel;
            var hiresSamplerCard = HiresSamplerCardViewModel;

            // Requested upscale to this size
            var hiresWidth = (int)Math.Floor(lastLatentWidth * hiresUpscalerCard.Scale);
            var hiresHeight = (int)Math.Floor(lastLatentHeight * hiresUpscalerCard.Scale);
            
            LatentNodeConnection hiresLatent;
            
            // Select between latent upscale and normal upscale based on the upscale method
            var selectedUpscaler = hiresUpscalerCard.SelectedUpscaler!.Value;
            
            if (selectedUpscaler.Type == ComfyUpscalerType.None)
            {
                // If no upscaler selected or none, just reroute the latent image
                hiresLatent = sampler.Output;
            }
            else
            {
                // Otherwise upscale the latent image
                hiresLatent = builder.Group_UpscaleToLatent("HiresFix",
                    lastLatent, vaeSource, selectedUpscaler, hiresWidth, hiresHeight).Output;
            }

            var hiresSampler = prompt.AddNamedNode(
                ComfyNodeBuilder.KSampler(
                    "HiresSampler",
                    checkpointLoader.GetOutput<ModelNodeConnection>(0),
                    Convert.ToUInt64(seedCard.Seed),
                    hiresSamplerCard.Steps,
                    hiresSamplerCard.CfgScale,
                    // Use hires sampler name if not null, otherwise use the normal sampler name
                    hiresSamplerCard.SelectedSampler?.Name
                        ?? samplerCard.SelectedSampler?.Name
                        ?? throw new InvalidOperationException("Sampler not selected"),
                    "normal",
                    positiveClip.GetOutput<ConditioningNodeConnection>(0),
                    negativeClip.GetOutput<ConditioningNodeConnection>(0),
                    hiresLatent,
                    hiresSamplerCard.DenoiseStrength
                )
            );

            // Set as last latent
            lastLatent = hiresSampler.Output;
            lastLatentWidth = hiresWidth;
            lastLatentHeight = hiresHeight;
            // Reroute the VAEDecoder's input to be from the hires sampler
            vaeDecoder.Inputs["samples"] = lastLatent;
        }
        
        // If upscale is enabled, add another upscale group
        if (IsUpscaleEnabled)
        {
            var postUpscalerCard = StackCardViewModel.GetCard<StackExpanderViewModel>(1)
                .GetCard<UpscalerCardViewModel>();
            
            var upscaleWidth = (int)Math.Floor(lastLatentWidth * postUpscalerCard.Scale);
            var upscaleHeight = (int)Math.Floor(lastLatentHeight * postUpscalerCard.Scale);
            
            // Build group
            var postUpscaleGroup = builder.Group_UpscaleToImage("PostUpscale",
                lastLatent, vaeSource, postUpscalerCard.SelectedUpscaler!.Value,
                upscaleWidth, upscaleHeight);
            
            // Remove the original vae decoder
            prompt.Remove(vaeDecoder.Name);
            
            // Set as the input for save image
            saveImage.Inputs["images"] = postUpscaleGroup.Output;
        }

        prompt.NormalizeConnectionTypes();

        return (prompt, new[] { saveImage.Name });
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
        Dispatcher.UIThread.Post(() =>
        {
            using var stream = new MemoryStream(args.ImageBytes);

            var bitmap = new Bitmap(stream);
            
            var currentImage = ImageGalleryCardViewModel.PreviewImage;
            
            ImageGalleryCardViewModel.PreviewImage = bitmap;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = true;
            
            currentImage?.Dispose();
        });
    }

    private async Task GenerateImageImpl(
        GenerateOverrides? overrides = null,
        CancellationToken cancellationToken = default)
    {
        if (!ClientManager.IsConnected)
        {
            notificationService.Show("Client not connected", "Please connect first");
            return;
        }

        // If enabled, randomize the seed
        var seedCard = StackCardViewModel.GetCard<SeedCardViewModel>();
        if (overrides is not { UseCurrentSeed: true } &&
            seedCard.IsRandomizeEnabled)
        {
            seedCard.GenerateNewSeed();
        }

        var client = ClientManager.Client;

        var (nodes, outputNodeNames) = BuildPrompt();

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

            var images = imageOutputs[outputNodeNames[0]];
            if (images is null)
                return;

            List<ImageSource> outputImages;
            // Use local file path if available, otherwise use remote URL
            if (client.OutputImagesDir is { } outputPath)
            {
                outputImages = images
                    .Select(i => new ImageSource(i.ToFilePath(outputPath)))
                    .ToList();
            }
            else
            {
                outputImages = images
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

                // Save to disk
                var lastName = outputImages.Last().LocalFile?.Info.Name;
                var gridPath = client.OutputImagesDir!.JoinFile($"grid-{lastName}");

                await using (var fileStream = gridPath.Info.OpenWrite())
                {
                    await fileStream.WriteAsync(grid.Encode().ToArray(), cancellationToken);
                }

                // Insert to start of images
                var gridImage = new ImageSource(gridPath);
                // Preload
                await gridImage.GetBitmapAsync();
                ImageGalleryCardViewModel.ImageSources.Add(gridImage);
            }

            // Add rest of images
            foreach (var img in outputImages)
            {
                // Preload
                await img.GetBitmapAsync();
                ImageGalleryCardViewModel.ImageSources.Add(img);
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

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateImage(string? options = null, CancellationToken cancellationToken = default)
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
