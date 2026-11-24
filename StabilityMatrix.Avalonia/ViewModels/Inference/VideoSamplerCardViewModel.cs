using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
[ManagedService]
[RegisterTransient<VideoSamplerCardViewModel>]
public class VideoSamplerCardViewModel : SamplerCardViewModel
{
    public VideoSamplerCardViewModel(
        IInferenceClientManager clientManager,
        IServiceManager<ViewModelBase> vmFactory,
        ISettingsManager settingsManager,
        TabContext tabContext
    )
        : base(clientManager, vmFactory, settingsManager, tabContext)
    {
        EnableAddons = false;
        IsLengthEnabled = true;
        SelectedSampler = ComfySampler.UniPC;
        SelectedScheduler = ComfyScheduler.Simple;
        Length = 33;
    }

    public override void ApplyStep(ModuleApplyStepEventArgs e)
    {
        if (EnableAddons)
        {
            foreach (var module in ModulesCardViewModel.Cards.OfType<ModuleBase>())
            {
                module.ApplyStep(e);
            }
        }

        // Set primary sampler and scheduler
        var primarySampler = SelectedSampler ?? throw new ValidationException("Sampler not selected");
        e.Builder.Connections.PrimarySampler = primarySampler;

        var primaryScheduler = SelectedScheduler ?? throw new ValidationException("Scheduler not selected");
        e.Builder.Connections.PrimaryScheduler = primaryScheduler;

        // for later inheritance if needed
        e.Builder.Connections.PrimaryCfg = CfgScale;
        e.Builder.Connections.PrimarySteps = Steps;
        e.Builder.Connections.PrimaryModelType = SelectedModelType;

        e.Temp = e.CreateTempFromBuilder();

        var conditioning = e.Temp.Base.Conditioning.Unwrap();

        // Builds the final sampler for the given conditioning + latent. Runs a single KSampler, or,
        // when a Wan 2.2 14B low-noise expert is loaded, a two-pass high-noise -> low-noise KSamplerAdvanced.
        LatentNodeConnection BuildVideoSampler(
            ConditioningNodeConnection positive,
            ConditioningNodeConnection negative,
            LatentNodeConnection latentImage
        )
        {
            var highNoiseModel = e.Temp.Base.Model!.Unwrap();
            var lowNoiseModel = e.Builder.Connections.WanLowNoiseModel;

            if (lowNoiseModel is null)
            {
                var kSampler = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.KSampler
                    {
                        Name = "Sampler",
                        Model = highNoiseModel,
                        Seed = e.Builder.Connections.Seed,
                        SamplerName = primarySampler.Name,
                        Scheduler = primaryScheduler.Name,
                        Steps = Steps,
                        Cfg = CfgScale,
                        Positive = positive,
                        Negative = negative,
                        LatentImage = latentImage,
                        Denoise = DenoiseStrength,
                    }
                );
                return kSampler.Output;
            }

            // Wan 2.2 14B MoE: high-noise expert handles the first fraction of steps, low-noise the rest.
            var boundary = e.Builder.Connections.WanMoeBoundary ?? 0.5d;
            var switchStep = Math.Clamp((int)Math.Round(Steps * boundary), 1, Math.Max(1, Steps - 1));

            var highNoisePass = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSamplerAdvanced
                {
                    Name = e.Nodes.GetUniqueName("WanHighNoiseSampler"),
                    Model = highNoiseModel,
                    AddNoise = true,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Steps = Steps,
                    Cfg = CfgScale,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Positive = positive,
                    Negative = negative,
                    LatentImage = latentImage,
                    StartAtStep = 0,
                    EndAtStep = switchStep,
                    ReturnWithLeftoverNoise = true,
                }
            );

            var lowNoisePass = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSamplerAdvanced
                {
                    Name = "Sampler",
                    Model = lowNoiseModel,
                    AddNoise = false,
                    NoiseSeed = e.Builder.Connections.Seed,
                    Steps = Steps,
                    Cfg = CfgScale,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Positive = positive,
                    Negative = negative,
                    LatentImage = highNoisePass.Output,
                    StartAtStep = switchStep,
                    EndAtStep = Steps,
                    ReturnWithLeftoverNoise = false,
                }
            );
            return lowNoisePass.Output;
        }

        // When the tab already built the family's conditioning + latent (MiniMax H3), the image
        // input was consumed there — sample what's present instead of the Wan i2v conditioning.
        var isImgToVid = IsDenoiseStrengthEnabled && !e.Builder.Connections.HasPrebuiltVideoConditioning;

        if (isImgToVid)
        {
            var clipVisionEncode = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.CLIPVisionEncode
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPVisionEncode)),
                    ClipVision = e.Builder.Connections.BaseClipVision
                        ?? throw new ValidationException("BaseClipVision not set"),
                    Image = e.Builder.GetPrimaryAsImage(),
                    Crop = "none",
                }
            );

            var wanImageToVideo = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.WanImageToVideo
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.WanImageToVideo)),
                    Positive = conditioning.Positive,
                    Negative = conditioning.Negative,
                    Vae = e.Builder.Connections.GetDefaultVAE(),
                    ClipVisionOutput = clipVisionEncode.Output,
                    StartImage = e.Builder.GetPrimaryAsImage(),
                    Width = Width,
                    Height = Height,
                    Length = Length,
                    BatchSize = e.Builder.Connections.BatchSize,
                }
            );

            e.Builder.Connections.Primary = BuildVideoSampler(
                wanImageToVideo.Output1,
                wanImageToVideo.Output2,
                wanImageToVideo.Output3
            );
        }
        else
        {
            var primaryLatent = e.Builder.GetPrimaryAsLatent(
                e.Temp.Primary!.Unwrap(),
                e.Builder.Connections.GetDefaultVAE()
            );

            e.Builder.Connections.Primary = BuildVideoSampler(
                conditioning.Positive,
                conditioning.Negative,
                primaryLatent
            );
        }
    }
}
