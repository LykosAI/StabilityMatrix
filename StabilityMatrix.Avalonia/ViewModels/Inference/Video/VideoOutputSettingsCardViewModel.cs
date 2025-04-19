using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Video;

[View(typeof(VideoOutputSettingsCard))]
[ManagedService]
[RegisterTransient<VideoOutputSettingsCardViewModel>]
public partial class VideoOutputSettingsCardViewModel
    : LoadableViewModelBase,
        IParametersLoadableState,
        IComfyStep
{
    [ObservableProperty]
    private double fps = 6;

    [ObservableProperty]
    private bool lossless = true;

    [ObservableProperty]
    private int quality = 85;

    [ObservableProperty]
    private VideoOutputMethod selectedMethod = VideoOutputMethod.Default;

    [ObservableProperty]
    private List<VideoOutputMethod> availableMethods = Enum.GetValues<VideoOutputMethod>().ToList();

    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        Fps = parameters.OutputFps;
        Lossless = parameters.Lossless;
        Quality = parameters.VideoQuality;

        if (string.IsNullOrWhiteSpace(parameters.VideoOutputMethod))
            return;

        SelectedMethod = Enum.TryParse<VideoOutputMethod>(parameters.VideoOutputMethod, true, out var method)
            ? method
            : VideoOutputMethod.Default;
    }

    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            OutputFps = Fps,
            Lossless = Lossless,
            VideoQuality = Quality,
            VideoOutputMethod = SelectedMethod.ToString(),
        };
    }

    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        if (e.Builder.Connections.Primary is null)
            throw new ArgumentException("No Primary");

        var image = e.Builder.Connections.Primary.Match(
            _ =>
                e.Builder.GetPrimaryAsImage(
                    e.Builder.Connections.PrimaryVAE
                        ?? e.Builder.Connections.Refiner.VAE
                        ?? e.Builder.Connections.Base.VAE
                        ?? throw new ArgumentException("No Primary, Refiner, or Base VAE")
                ),
            image => image
        );

        var outputStep = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.SaveAnimatedWEBP
            {
                Name = e.Nodes.GetUniqueName("SaveAnimatedWEBP"),
                Images = image,
                FilenamePrefix = "InferenceVideo",
                Fps = Fps,
                Lossless = Lossless,
                Quality = Quality,
                Method = SelectedMethod.ToString().ToLowerInvariant()
            }
        );

        e.Builder.Connections.OutputNodes.Add(outputStep);
    }
}
