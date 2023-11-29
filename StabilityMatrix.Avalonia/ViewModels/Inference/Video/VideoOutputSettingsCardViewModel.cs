using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
[Transient]
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
        // TODO
    }

    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        // TODO
        return parameters;
    }

    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        if (e.Builder.Connections.Primary is null)
            throw new ArgumentException("No Primary");

        var image = e.Builder.Connections.Primary.Match(
            _ =>
                e.Builder.GetPrimaryAsImage(
                    e.Builder.Connections.PrimaryVAE
                        ?? e.Builder.Connections.RefinerVAE
                        ?? e.Builder.Connections.BaseVAE
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
