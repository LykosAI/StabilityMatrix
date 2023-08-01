using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(InferenceTextToImageView))]
public partial class InferenceTextToImageViewModel : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public InferenceViewModel? Parent { get; set; }
    public SeedCardViewModel SeedCardViewModel { get; init; } = new();
    public SamplerCardViewModel SamplerCardViewModel { get; init; } = new();
    
    public TextDocument PromptDocument { get; } = new();
    public TextDocument NegativePromptDocument { get; } = new();

    [ObservableProperty] private string? selectedModelName;

    [ObservableProperty] private int progressCurrent;
    [ObservableProperty] private int progressMax;

    [ObservableProperty] private string? outputImageSource;

    public InferenceTextToImageViewModel()
    {
        SetDefaults();
    }
    
    private void SetDefaults()
    {
        SelectedModelName = "v1-5-pruned-emaonly.safetensors";
        SeedCardViewModel.Seed = Random.Shared.NextInt64();
        SamplerCardViewModel.SelectedSampler = "euler";
        SamplerCardViewModel.Height = 512;
        SamplerCardViewModel.Width = 512;
        SamplerCardViewModel.CfgScale = 7.0;
        SamplerCardViewModel.Steps = 20;
    }
    
    private Dictionary<string, ComfyNode> GetCurrentPrompt()
    {
        var prompt = new Dictionary<string, ComfyNode>()
        {
            ["3"] = new ComfyNode()
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
                    ["steps"] = 20
                }
            },
            ["4"] = new ComfyNode()
            {
                ClassType = "CheckpointLoaderSimple",
                Inputs = new Dictionary<string, object?>
                {
                    ["ckpt_name"] = SelectedModelName
                }
            },
            ["5"] = new ComfyNode()
            {
                ClassType = "EmptyLatentImage",
                Inputs = new Dictionary<string, object?>
                {
                    ["batch_size"] = 1,
                    ["height"] = SamplerCardViewModel.Height,
                    ["width"] = SamplerCardViewModel.Width,
                }
            },
            ["6"] = new ComfyNode()
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
            ["7"] = new ComfyNode()
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
            ["8"] = new ComfyNode()
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
            ["9"] = new ComfyNode()
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

    [RelayCommand]
    private async Task GenerateImage()
    {
        var nodes = GetCurrentPrompt();

        var client = Parent!.Client!;
        
        // Progress handler
        var progress = new Progress<ProgressReport>(report =>
        {
            ProgressCurrent = Convert.ToInt32(report.Current);
            ProgressMax = Convert.ToInt32(report.Total);
        });

        var results = await client.ExecutePromptAsync(nodes, progress);
        
        Logger.Info(results);
    }
}
