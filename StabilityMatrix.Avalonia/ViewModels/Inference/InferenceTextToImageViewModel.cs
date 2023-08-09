using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using SkiaSharp;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceTextToImageView))]
public partial class InferenceTextToImageViewModel
    : ViewModelBase,
        ILoadableState<InferenceTextToImageModel>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    public IInferenceClientManager ClientManager { get; }

    public SeedCardViewModel SeedCardViewModel { get; }
    public SamplerCardViewModel SamplerCardViewModel { get; }
    public SamplerCardViewModel HiresFixSamplerCardViewModel { get; }
    public ImageGalleryCardViewModel ImageGalleryCardViewModel { get; }
    public PromptCardViewModel PromptCardViewModel { get; }
    public StackCardViewModel ConfigCardViewModel { get; }

    [ObservableProperty]
    private string? selectedModelName;

    [ObservableProperty] 
    private int batchSize = 1;

    [ObservableProperty]
    private int batchCount = 1;

    public ProgressViewModel OutputProgress { get; } = new();

    [ObservableProperty]
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
        
        SeedCardViewModel = vmFactory.Get<SeedCardViewModel>();        
        SeedCardViewModel.GenerateNewSeed();
        // SamplerCardViewModel = vmFactory.Get<SamplerCardViewModel>();
        HiresFixSamplerCardViewModel = vmFactory.Get<SamplerCardViewModel>(vm =>
        {
            vm.IsScaleSizeMode = true;
            vm.IsCfgScaleEnabled = false;
            vm.IsSamplerSelectionEnabled = false;
            vm.IsDenoiseStrengthEnabled = true;
        });
        ImageGalleryCardViewModel = vmFactory.Get<ImageGalleryCardViewModel>();
        PromptCardViewModel = vmFactory.Get<PromptCardViewModel>();

        ConfigCardViewModel = vmFactory.Get<StackCardViewModel>().WithCards(new ViewModelBase[]
            {
                vmFactory.Get<SamplerCardViewModel>(),
                vmFactory.Get<StackExpanderViewModel>().WithCards(new ViewModelBase[]
                {
                    vmFactory.Get<UpscalerCardViewModel>(),
                    vmFactory.Get<SamplerCardViewModel>(vm =>
                    {
                        vm.IsScaleSizeMode = true;
                        vm.IsCfgScaleEnabled = false;
                        vm.IsSamplerSelectionEnabled = false;
                        vm.IsDenoiseStrengthEnabled = true;
                    }),
                })
            });
    }

    private Dictionary<string, ComfyNode> GetCurrentPrompt()
    {
        var prompt = new Dictionary<string, ComfyNode>
        {
            ["3"] = new()
            {
                ClassType = "KSampler",
                Inputs = new Dictionary<string, object?>
                {
                    ["cfg"] = SamplerCardViewModel.CfgScale,
                    ["denoise"] = 1,
                    ["latent_image"] = new object[] { "5", 0 },
                    ["model"] = new object[] { "4", 0 },
                    ["negative"] = new object[] { "7", 0 },
                    ["positive"] = new object[] { "6", 0 },
                    ["sampler_name"] = SamplerCardViewModel.SelectedSampler?.Name,
                    ["scheduler"] = "normal",
                    ["seed"] = SeedCardViewModel.Seed,
                    ["steps"] = SamplerCardViewModel.Steps
                }
            },
            ["4"] = new()
            {
                ClassType = "CheckpointLoaderSimple",
                Inputs = new Dictionary<string, object?> { ["ckpt_name"] = SelectedModelName }
            },
            ["5"] = new()
            {
                ClassType = "EmptyLatentImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["batch_size"] = BatchSize,
                    ["height"] = SamplerCardViewModel.Height,
                    ["width"] = SamplerCardViewModel.Width,
                }
            },
            ["6"] = new()
            {
                ClassType = "CLIPTextEncode",
                Inputs = new Dictionary<string, object?>
                {
                    ["clip"] = new object[] { "4", 1 },
                    ["text"] = PromptCardViewModel.PromptDocument.Text,
                }
            },
            ["7"] = new()
            {
                ClassType = "CLIPTextEncode",
                Inputs = new Dictionary<string, object?>
                {
                    ["clip"] = new object[] { "4", 1 },
                    ["text"] = PromptCardViewModel.NegativePromptDocument.Text,
                }
            },
            ["8"] = new()
            {
                ClassType = "VAEDecode",
                Inputs = new Dictionary<string, object?>
                {
                    ["samples"] = new object[] { "3", 0 },
                    ["vae"] = new object[] { "4", 2 }
                }
            },
            ["9"] = new()
            {
                ClassType = "SaveImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["filename_prefix"] = "SM-Inference",
                    ["images"] = new object[] { "8", 0 }
                }
            }
        };
        return prompt;
    }

    private void OnProgressUpdateReceived(object? sender, ComfyWebSocketProgressData args)
    {
        OutputProgress.Value = args.Value;
        OutputProgress.Maximum = args.Max;
        OutputProgress.IsIndeterminate = false;
    }

    private void OnPreviewImageReceived(object? sender, ComfyWebSocketImageData args)
    {
        // Decode to bitmap
        using var stream = new MemoryStream(args.ImageBytes);
        var bitmap = new Bitmap(stream);

        ImageGalleryCardViewModel.PreviewImage?.Dispose();
        ImageGalleryCardViewModel.PreviewImage = bitmap;
        ImageGalleryCardViewModel.IsPreviewOverlayEnabled = true;
    }

    private async Task GenerateImageImpl(CancellationToken cancellationToken = default)
    {
        if (!ClientManager.IsConnected)
        {
            notificationService.Show("Client not connected", "Please connect first");
            return;
        }

        // If enabled, randomize the seed
        if (SeedCardViewModel.IsRandomizeEnabled)
        {
            SeedCardViewModel.GenerateNewSeed();
        }

        var client = ClientManager.Client;

        var nodes = GetCurrentPrompt();

        // Connect progress handler
        client.ProgressUpdateReceived += OnProgressUpdateReceived;
        client.PreviewImageReceived += OnPreviewImageReceived;

        try
        {
            var (response, promptTask) = await client.QueuePromptAsync(nodes, cancellationToken);
            Logger.Info(response);

            // Wait for prompt to finish
            await promptTask.WaitAsync(cancellationToken);
            Logger.Trace($"Prompt task {response.PromptId} finished");

            // Get output images
            var outputs = await client.GetImagesForExecutedPromptAsync(
                response.PromptId,
                cancellationToken
            );

            ImageGalleryCardViewModel.ImageSources.Clear();
            
            // Only get the SaveImage images from node 9
            var images = outputs["9"];
            if (images is null) return;

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
                var loadedImages = outputImages.Select(i =>
                    SKImage.FromEncodedData(i.LocalFile?.Info.OpenRead())).ToImmutableArray();

                var grid = ImageProcessor.CreateImageGrid(loadedImages);
                
                // Save to disk
                var lastName = outputImages.Last().LocalFile?.Info.Name;
                var gridPath = client.OutputImagesDir!.JoinFile($"grid-{lastName}");

                await using var fileStream = gridPath.Info.OpenWrite();
                await fileStream.WriteAsync(grid.Encode().ToArray(), cancellationToken);
                
                // Insert to start of gallery
                ImageGalleryCardViewModel.ImageSources.Add(new ImageSource(gridPath));
                // var bitmaps = (await outputImages.SelectAsync(async i => await i.GetBitmapAsync())).ToImmutableArray();
            }
            
            // Insert rest of images
            ImageGalleryCardViewModel.ImageSources.AddRange(outputImages);
        }
        finally
        {
            // Disconnect progress handler
            OutputProgress.Value = 0;
            ImageGalleryCardViewModel.PreviewImage?.Dispose();
            ImageGalleryCardViewModel.PreviewImage = null;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = false;
            client.ProgressUpdateReceived -= OnProgressUpdateReceived;
            client.PreviewImageReceived -= OnPreviewImageReceived;
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateImage(CancellationToken cancellationToken = default)
    {
        try
        {
            await GenerateImageImpl(cancellationToken);
        }
        catch (OperationCanceledException e)
        {
            Logger.Debug($"[Image Generation Canceled] {e.Message}");
        }
    }

    /// <inheritdoc />
    public void LoadState(InferenceTextToImageModel state)
    {
        SelectedModelName = state.SelectedModelName;

        if (state.SeedCardState != null)
        {
            SeedCardViewModel.LoadState(state.SeedCardState);
        }
        if (state.SamplerCardState != null)
        {
            SamplerCardViewModel.LoadState(state.SamplerCardState);
        }
        if (state.PromptCardState != null)
        {
            PromptCardViewModel.LoadState(state.PromptCardState);
        }
    }

    /// <inheritdoc />
    public InferenceTextToImageModel SaveState()
    {
        return new InferenceTextToImageModel
        {
            SelectedModelName = SelectedModelName,
            SeedCardState = SeedCardViewModel.SaveState(),
            SamplerCardState = SamplerCardViewModel.SaveState(),
            PromptCardState = PromptCardViewModel.SaveState(),
        };
    }
}
