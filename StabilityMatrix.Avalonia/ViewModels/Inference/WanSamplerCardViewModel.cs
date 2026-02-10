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
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(SamplerCard))]
[ManagedService]
[RegisterTransient<WanSamplerCardViewModel>]
public class WanSamplerCardViewModel : SamplerCardViewModel
{
    public WanSamplerCardViewModel(
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

        var isImgToVid = IsDenoiseStrengthEnabled;

        if (isImgToVid)
        {
            var clipVisionEncode = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.CLIPVisionEncode
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPVisionEncode)),
                    ClipVision =
                        e.Builder.Connections.BaseClipVision
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

            var kSampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSampler
                {
                    Name = "Sampler",
                    Model = e.Temp.Base.Model!.Unwrap(),
                    Seed = e.Builder.Connections.Seed,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Steps = Steps,
                    Cfg = CfgScale,
                    Positive = wanImageToVideo.Output1,
                    Negative = wanImageToVideo.Output2,
                    LatentImage = wanImageToVideo.Output3,
                    Denoise = DenoiseStrength,
                }
            );
            e.Builder.Connections.Primary = kSampler.Output;
        }
        else
        {
            var primaryLatent = e.Builder.GetPrimaryAsLatent(
                e.Temp.Primary!.Unwrap(),
                e.Builder.Connections.GetDefaultVAE()
            );

            var kSampler = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSampler
                {
                    Name = "Sampler",
                    Model = e.Temp.Base.Model!.Unwrap(),
                    Seed = e.Builder.Connections.Seed,
                    SamplerName = primarySampler.Name,
                    Scheduler = primaryScheduler.Name,
                    Steps = Steps,
                    Cfg = CfgScale,
                    Positive = conditioning.Positive,
                    Negative = conditioning.Negative,
                    LatentImage = primaryLatent,
                    Denoise = DenoiseStrength,
                }
            );
            e.Builder.Connections.Primary = kSampler.Output;
        }
    }
}
