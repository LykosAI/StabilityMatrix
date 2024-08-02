using System;
using System.Threading.Tasks;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[Transient]
public class FaceDetailerModule : ModuleBase, IValidatableModule
{
    public FaceDetailerModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Face Detailer";
        AddCards(vmFactory.Get<FaceDetailerViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var faceDetailerCard = GetCard<FaceDetailerViewModel>();
        if (faceDetailerCard is { InheritSeed: false, SeedCardViewModel.IsRandomizeEnabled: true })
        {
            faceDetailerCard.SeedCardViewModel.GenerateNewSeed();
        }

        var bboxLoader = new ComfyNodeBuilder.UltralyticsDetectorProvider
        {
            Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UltralyticsDetectorProvider)),
            ModelName =
                GetModelName(faceDetailerCard.BboxModel) ?? throw new ArgumentException("No BboxModel"),
        };

        var faceDetailer = new ComfyNodeBuilder.FaceDetailer
        {
            Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.FaceDetailer)),
            GuideSize = faceDetailerCard.GuideSize,
            GuideSizeFor = faceDetailerCard.GuideSizeFor,
            MaxSize = faceDetailerCard.MaxSize,
            Seed = faceDetailerCard.InheritSeed
                ? e.Builder.Connections.Seed
                : Convert.ToUInt64(faceDetailerCard.SeedCardViewModel.Seed),
            Steps = faceDetailerCard.Steps,
            Cfg = faceDetailerCard.Cfg,
            SamplerName =
                faceDetailerCard.Sampler?.Name
                ?? e.Builder.Connections.PrimarySampler?.Name
                ?? throw new ArgumentException("No PrimarySampler"),
            Scheduler =
                faceDetailerCard.Scheduler?.Name
                ?? e.Builder.Connections.PrimaryScheduler?.Name
                ?? throw new ArgumentException("No PrimaryScheduler"),
            Denoise = faceDetailerCard.Denoise,
            Feather = faceDetailerCard.Feather,
            NoiseMask = faceDetailerCard.NoiseMask,
            ForceInpaint = faceDetailerCard.ForceInpaint,
            BboxThreshold = faceDetailerCard.BboxThreshold,
            BboxDilation = faceDetailerCard.BboxDilation,
            BboxCropFactor = faceDetailerCard.BboxCropFactor,
            SamDetectionHint = faceDetailerCard.SamDetectionHint,
            SamDilation = faceDetailerCard.SamDilation,
            SamThreshold = faceDetailerCard.SamThreshold,
            SamBboxExpansion = faceDetailerCard.SamBboxExpansion,
            SamMaskHintThreshold = faceDetailerCard.SamMaskHintThreshold,
            SamMaskHintUseNegative = faceDetailerCard.SamMaskHintUseNegative,
            DropSize = faceDetailerCard.DropSize,
            Cycle = faceDetailerCard.Cycle,
            Image = e.Builder.GetPrimaryAsImage(),
            Model = e.Builder.Connections.GetRefinerOrBaseModel(),
            Clip = e.Builder.Connections.Base.Clip ?? throw new ArgumentException("No BaseClip"),
            Vae = e.Builder.Connections.GetDefaultVAE(),
            Positive = GetPositiveConditioning(faceDetailerCard, e),
            Negative = GetNegativeConditioning(faceDetailerCard, e),
            BboxDetector = e.Nodes.AddTypedNode(bboxLoader).Output1,
            Wildcard = new StringNodeConnection() // TODO put <lora:stuff:here>
        };

        var segmModelName = GetModelName(faceDetailerCard.SegmModel);
        if (!string.IsNullOrWhiteSpace(segmModelName))
        {
            var segmLoader = new ComfyNodeBuilder.UltralyticsDetectorProvider
            {
                Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UltralyticsDetectorProvider)),
                ModelName = segmModelName
            };
            faceDetailer.SegmDetectorOpt = e.Nodes.AddTypedNode(segmLoader).Output2;
        }

        var samModelName = GetModelName(faceDetailerCard.SamModel);
        if (!string.IsNullOrWhiteSpace(samModelName))
        {
            var samLoader = new ComfyNodeBuilder.SamLoader
            {
                Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.SamLoader)),
                ModelName = samModelName,
                DeviceMode = "AUTO",
            };
            faceDetailer.SamModelOpt = e.Nodes.AddTypedNode(samLoader).Output;
        }

        e.Builder.Connections.Primary = e.Nodes.AddTypedNode(faceDetailer).Output;
    }

    private string? GetModelName(HybridModelFile? model) =>
        model switch
        {
            null => null,
            { FileName: "@none" } => null,
            { RemoteName: "@none" } => null,
            { Local: not null } => model.RelativePath.NormalizePathSeparators(),
            { RemoteName: not null } => model.RemoteName,
            _ => null
        };

    private ConditioningNodeConnection GetPositiveConditioning(
        FaceDetailerViewModel viewModel,
        ModuleApplyStepEventArgs e
    )
    {
        if (!viewModel.UseSeparatePrompt)
        {
            return e.Builder.Connections.GetRefinerOrBaseConditioning().Positive;
        }

        var prompt = viewModel.PromptCardViewModel.GetPrompt();
        prompt.Process();
        var positiveClip = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPTextEncode)),
                Clip = e.Builder.Connections.Base.Clip!,
                Text = prompt.ProcessedText
            }
        );

        return positiveClip.Output;
    }

    private ConditioningNodeConnection GetNegativeConditioning(
        FaceDetailerViewModel viewModel,
        ModuleApplyStepEventArgs e
    )
    {
        if (!viewModel.UseSeparatePrompt)
        {
            return e.Builder.Connections.GetRefinerOrBaseConditioning().Negative;
        }

        var prompt = viewModel.PromptCardViewModel.GetNegativePrompt();
        prompt.Process();
        var negativeClip = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPTextEncode
            {
                Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPTextEncode)),
                Clip = e.Builder.Connections.Base.Clip!,
                Text = prompt.ProcessedText
            }
        );

        return negativeClip.Output;
    }

    public async Task<bool> Validate()
    {
        var faceDetailerCard = GetCard<FaceDetailerViewModel>();
        if (!string.IsNullOrWhiteSpace(GetModelName(faceDetailerCard.BboxModel)))
            return true;

        var dialog = DialogHelper.CreateMarkdownDialog(
            "Please select a BBox Model to continue.",
            "No Model Selected"
        );
        await dialog.ShowAsync();
        return false;
    }
}
