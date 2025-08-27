using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<FaceDetailerModule>]
public partial class FaceDetailerModule : ModuleBase, IValidatableModule
{
    /// <inheritdoc />
    public override bool IsSettingsEnabled => true;

    /// <inheritdoc />
    public override IRelayCommand SettingsCommand => OpenSettingsDialogCommand;

    public FaceDetailerModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Face Detailer";
        AddCards(vmFactory.Get<FaceDetailerViewModel>());
    }

    [RelayCommand]
    private async Task OpenSettingsDialog()
    {
        var gridVm = VmFactory.Get<PropertyGridViewModel>(vm =>
        {
            vm.Title = $"{Title} {Resources.Label_Settings}";
            vm.SelectedObject = Cards.ToArray();
            vm.IncludeCategories = ["Settings"];
        });

        await gridVm.GetDialog().ShowAsync();
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

        var samplerName =
            (
                faceDetailerCard.IsSamplerSelectionEnabled
                    ? faceDetailerCard.Sampler?.Name
                    : e.Builder.Connections.PrimarySampler?.Name
            ) ?? throw new ArgumentException("No PrimarySampler");

        var schedulerName =
            (
                faceDetailerCard.IsSchedulerSelectionEnabled
                    ? faceDetailerCard.Scheduler?.Name
                    : e.Builder.Connections.PrimaryScheduler?.Name
            ) ?? throw new ArgumentException("No PrimaryScheduler");

        if (schedulerName == "align_your_steps")
        {
            if (e.Builder.Connections.PrimaryModelType is null)
            {
                throw new ArgumentException("No Model Type for AYS");
            }

            schedulerName =
                e.Builder.Connections.PrimaryModelType == "SDXL"
                    ? ComfyScheduler.FaceDetailerAlignYourStepsSDXL.Name
                    : ComfyScheduler.FaceDetailerAlignYourStepsSD1.Name;
        }

        var cfg = faceDetailerCard.IsCfgScaleEnabled
            ? faceDetailerCard.Cfg
            : e.Builder.Connections.PrimaryCfg ?? throw new ArgumentException("No CFG");

        var steps = faceDetailerCard.IsStepsEnabled
            ? faceDetailerCard.Steps
            : e.Builder.Connections.PrimarySteps ?? throw new ArgumentException("No Steps");

        var faceDetailer = new ComfyNodeBuilder.FaceDetailer
        {
            Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.FaceDetailer)),
            GuideSize = faceDetailerCard.GuideSize,
            GuideSizeFor = faceDetailerCard.GuideSizeFor,
            MaxSize = faceDetailerCard.MaxSize,
            Seed = faceDetailerCard.InheritSeed
                ? e.Builder.Connections.Seed
                : Convert.ToUInt64(faceDetailerCard.SeedCardViewModel.Seed),
            Steps = steps,
            Cfg = cfg,
            SamplerName = samplerName,
            Scheduler = schedulerName,
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
            Wildcard = faceDetailerCard.WildcardViewModel.GetPrompt().ProcessedText ?? string.Empty,
            TiledDecode = faceDetailerCard.UseTiledDecode,
            TiledEncode = faceDetailerCard.UseTiledEncode,
        };

        var segmModelName = GetModelName(faceDetailerCard.SegmModel);
        if (!string.IsNullOrWhiteSpace(segmModelName))
        {
            var segmLoader = new ComfyNodeBuilder.UltralyticsDetectorProvider
            {
                Name = e.Builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UltralyticsDetectorProvider)),
                ModelName = segmModelName,
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
            _ => null,
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
                Text = prompt.ProcessedText,
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
                Text = prompt.ProcessedText,
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
