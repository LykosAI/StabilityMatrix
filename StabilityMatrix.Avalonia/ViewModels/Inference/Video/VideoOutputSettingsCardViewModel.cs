using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
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

    /// <summary>
    /// Set by the owning tab when the model family's output only fits a real video container
    /// (<see cref="Models.Inference.VideoWorkflowProfile.RequiresContainerFormat"/>): the
    /// format locks to mp4, including a coercion of whatever a saved project carried.
    /// Not persisted — it derives from the tab's resolved profile.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool isContainerFormatRequired;

    partial void OnIsContainerFormatRequiredChanged(bool value)
    {
        if (value)
        {
            AvailableFormats = [VideoOutputFormat.Mp4];
            SelectedFormat = VideoOutputFormat.Mp4;
        }
        else
        {
            AvailableFormats = Enum.GetValues<VideoOutputFormat>().ToList();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebPFormat))]
    private VideoOutputFormat selectedFormat = VideoOutputFormat.WebP;

    [ObservableProperty]
    private List<VideoOutputFormat> availableFormats = Enum.GetValues<VideoOutputFormat>().ToList();

    /// <summary>WebP-specific controls (lossless / quality / method) only apply to the WebP format.</summary>
    public bool IsWebPFormat => SelectedFormat == VideoOutputFormat.WebP;

    partial void OnSelectedFormatChanged(VideoOutputFormat value)
    {
        // A loaded project can carry a non-container format from before the profile resolved
        // (or from a different family) — the lock wins.
        if (IsContainerFormatRequired && value != VideoOutputFormat.Mp4)
        {
            SelectedFormat = VideoOutputFormat.Mp4;
        }
    }

    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        Fps = parameters.OutputFps;
        Lossless = parameters.Lossless;
        Quality = parameters.VideoQuality;

        if (
            !string.IsNullOrWhiteSpace(parameters.VideoOutputFormat)
            && Enum.TryParse<VideoOutputFormat>(parameters.VideoOutputFormat, true, out var format)
        )
        {
            SelectedFormat = format;
        }

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
            VideoOutputFormat = SelectedFormat.ToString(),
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

        switch (SelectedFormat)
        {
            case VideoOutputFormat.Mp4:
            {
                // Families with native audio (MiniMax H3) leave an audio VAE on the builder;
                // decode the soundtrack from the sampled packed latent and mux it in. WebP
                // below has no audio track, so this stays mp4-only.
                ComfyNodeBuilder.VAEDecodeAudio? audioDecode = null;
                if (
                    e.Builder.Connections.AudioVAE is { } audioVae
                    && e.Builder.Connections.LastPrimaryLatent is { } sampledLatent
                )
                {
                    audioDecode = e.Nodes.AddTypedNode(
                        new ComfyNodeBuilder.VAEDecodeAudio
                        {
                            Name = e.Nodes.GetUniqueName("VAEDecodeAudio"),
                            Samples = sampledLatent,
                            Vae = audioVae,
                        }
                    );
                }

                // Core ComfyUI mp4 path: assemble frames into a VIDEO, then encode to a container.
                var createVideo = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.CreateVideo
                    {
                        Name = e.Nodes.GetUniqueName("CreateVideo"),
                        Images = image,
                        Fps = Fps,
                        Audio = audioDecode?.Output,
                    }
                );

                var saveVideo = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.SaveVideo
                    {
                        Name = e.Nodes.GetUniqueName("SaveVideo"),
                        Video = createVideo.Output,
                        FilenamePrefix = "InferenceVideo",
                        Format = "mp4",
                        Codec = "h264",
                    }
                );

                e.Builder.Connections.OutputNodes.Add(saveVideo);
                break;
            }
            default:
            {
                var outputStep = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.SaveAnimatedWEBP
                    {
                        Name = e.Nodes.GetUniqueName("SaveAnimatedWEBP"),
                        Images = image,
                        FilenamePrefix = "InferenceVideo",
                        Fps = Fps,
                        Lossless = Lossless,
                        Quality = Quality,
                        Method = SelectedMethod.ToString().ToLowerInvariant(),
                    }
                );

                e.Builder.Connections.OutputNodes.Add(outputStep);
                break;
            }
        }
    }
}
