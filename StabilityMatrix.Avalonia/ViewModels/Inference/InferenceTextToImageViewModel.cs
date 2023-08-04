using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Bases;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.WebSocketData;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceTextToImageView))]
public partial class InferenceTextToImageViewModel : ViewModelBase, ILoadableState<InferenceTextToImageModel>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    public IInferenceClientManager ClientManager { get; }

    // These are set in OnLoaded due to needing the vmFactory
    [NotNull] public SeedCardViewModel? SeedCardViewModel { get; private set; }
    [NotNull] public SamplerCardViewModel? SamplerCardViewModel { get; private set; }
    [NotNull] public ImageGalleryCardViewModel? ImageGalleryCardViewModel { get; private set; }
    
    public InferenceViewModel? Parent { get; set; }
    
    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    [ObservableProperty]
    private string? selectedModelName;

    [ObservableProperty]
    private int batchSize = 1;
    
    public ProgressViewModel OutputProgress { get; } = new();
    
    [ObservableProperty]
    private string? outputImageSource;

    public InferenceTextToImageViewModel(
        INotificationService notificationService, 
        IInferenceClientManager inferenceClientManager,
        ServiceManager<ViewModelBase> vmFactory)
    {
        this.notificationService = notificationService;
        this.vmFactory = vmFactory;

        // ReSharper disable twice NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        SeedCardViewModel ??= vmFactory.Get<SeedCardViewModel>();
        OnPropertyChanged(nameof(SeedCardViewModel));
        SamplerCardViewModel ??= vmFactory.Get<SamplerCardViewModel>();
        OnPropertyChanged(nameof(SamplerCardViewModel));
        ImageGalleryCardViewModel ??= vmFactory.Get<ImageGalleryCardViewModel>();
        OnPropertyChanged(nameof(ImageGalleryCardViewModel));
        
        SeedCardViewModel.GenerateNewSeed();
        
        ClientManager = inferenceClientManager;
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
                    ["latent_image"] = new object[]
                    {
                        "5",
                        0
                    },
                    ["model"] = new object[]
                    {
                        "4",
                        0
                    },
                    ["negative"] = new object[]
                    {
                        "7",
                        0
                    },
                    ["positive"] = new object[]
                    {
                        "6",
                        0
                    },
                    ["sampler_name"] = SamplerCardViewModel.SelectedSampler,
                    ["scheduler"] = "normal",
                    ["seed"] = SeedCardViewModel.Seed,
                    ["steps"] = SamplerCardViewModel.Steps
                }
            },
            ["4"] = new()
            {
                ClassType = "CheckpointLoaderSimple",
                Inputs = new Dictionary<string, object?>
                {
                    ["ckpt_name"] = SelectedModelName
                }
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
                    ["clip"] = new object[]
                    {
                        "4",
                        1
                    },
                    ["text"] = PromptDocument.Text,
                }
            },
            ["7"] = new()
            {
                ClassType = "CLIPTextEncode",
                Inputs = new Dictionary<string, object?>
                {
                    ["clip"] = new object[]
                    {
                        "4",
                        1
                    },
                    ["text"] = NegativePromptDocument.Text,
                }
            },
            ["8"] = new()
            {
                ClassType = "VAEDecode",
                Inputs = new Dictionary<string, object?>
                {
                    ["samples"] = new object[]
                    {
                        "3",
                        0
                    },
                    ["vae"] = new object[]
                    {
                        "4",
                        2
                    }
                }
            },
            ["9"] = new()
            {
                ClassType = "SaveImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["filename_prefix"] = "SM-Inference",
                    ["images"] = new object[]
                    {
                        "8",
                        0
                    }
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
        ImageGalleryCardViewModel.PreviewImage = bitmap;
        ImageGalleryCardViewModel.IsPreviewOverlayEnabled = true;
    }

    [RelayCommand]
    private async Task GenerateImage()
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
        OutputProgress.IsIndeterminate = true;
        client.ProgressUpdateReceived += OnProgressUpdateReceived;
        client.PreviewImageReceived += OnPreviewImageReceived;
        
        try
        {
            var (response, promptTask) = await client.QueuePromptAsync(nodes);
            Logger.Info(response);

            // Wait for prompt to finish
            await promptTask;
            Logger.Trace($"Prompt task {response.PromptId} finished");

            // Get output images
            var outputs = await client.GetImagesForExecutedPromptAsync(response.PromptId);
            
            // Only get the SaveImage from node 9
            var images = outputs["9"];
            if (images is null) return;

            ImageGalleryCardViewModel.ImageSources.Clear();
            ImageGalleryCardViewModel.ImageSources.AddRange(
                images.Select(i => i.ToUri(client.BaseAddress).ToString()));
        }
        finally
        {
            // Disconnect progress handler
            OutputProgress.Value = 0;
            ImageGalleryCardViewModel.PreviewImage = null;
            ImageGalleryCardViewModel.IsPreviewOverlayEnabled = false;
            client.ProgressUpdateReceived -= OnProgressUpdateReceived;
            client.PreviewImageReceived -= OnPreviewImageReceived;
        }
    }

    /// <inheritdoc />
    public void LoadState(InferenceTextToImageModel state)
    {
        PromptDocument.Text = state.Prompt;
        NegativePromptDocument.Text = state.NegativePrompt;
        SelectedModelName = state.SelectedModelName;
        
        if (state.SeedCardState != null)
        {
            SeedCardViewModel.LoadState(state.SeedCardState);
        }
        if (state.SamplerCardState != null)
        {
            SamplerCardViewModel.LoadState(state.SamplerCardState);
        }
    }

    /// <inheritdoc />
    public InferenceTextToImageModel SaveState()
    {
        return new InferenceTextToImageModel
        {
            Prompt = PromptDocument.Text,
            NegativePrompt = NegativePromptDocument.Text,
            SelectedModelName = SelectedModelName,
            SeedCardState = SeedCardViewModel.SaveState(),
            SamplerCardState = SamplerCardViewModel.SaveState()
        };
    }
}
