using System;
using System.Linq;
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
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<HiresFixModule>]
public partial class HiresFixModule : ModuleBase
{
    /// <inheritdoc />
    public override bool IsSettingsEnabled => true;

    /// <inheritdoc />
    public override IRelayCommand SettingsCommand => OpenSettingsDialogCommand;

    /// <inheritdoc />
    public HiresFixModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "HiresFix";
        AddCards(
            vmFactory.Get<UpscalerCardViewModel>(),
            vmFactory.Get<SamplerCardViewModel>(vmSampler =>
            {
                vmSampler.IsDenoiseStrengthEnabled = true;
            })
        );
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

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var builder = e.Builder;

        var upscaleCard = GetCard<UpscalerCardViewModel>();
        var samplerCard = GetCard<SamplerCardViewModel>();

        // Get new latent size
        var hiresSize = builder.Connections.PrimarySize.WithScale(upscaleCard.Scale);

        // Select between latent upscale and normal upscale based on the upscale method
        var selectedUpscaler = upscaleCard.SelectedUpscaler!.Value;

        // If upscaler selected, upscale latent image first
        if (selectedUpscaler.Type != ComfyUpscalerType.None)
        {
            builder.Connections.Primary = builder.Group_Upscale(
                builder.Nodes.GetUniqueName("HiresFix"),
                builder.Connections.Primary.Unwrap(),
                builder.Connections.GetDefaultVAE(),
                selectedUpscaler,
                hiresSize.Width,
                hiresSize.Height
            );
        }

        // If we need to inherit primary sampler addons, use their temp args
        if (samplerCard.InheritPrimarySamplerAddons)
        {
            e.Temp = e.Builder.Connections.BaseSamplerTemporaryArgs ?? e.CreateTempFromBuilder();
        }
        else
        {
            // otherwise just use new ones
            e.Temp = e.CreateTempFromBuilder();
        }

        var samplerName =
            (
                samplerCard.IsSamplerSelectionEnabled
                    ? samplerCard.SelectedSampler?.Name
                    : e.Builder.Connections.PrimarySampler?.Name
            ) ?? throw new ArgumentException("No PrimarySampler");

        var schedulerName =
            (
                samplerCard.IsSchedulerSelectionEnabled
                    ? samplerCard.SelectedScheduler?.Name
                    : e.Builder.Connections.PrimaryScheduler?.Name
            ) ?? throw new ArgumentException("No PrimaryScheduler");

        var cfg = samplerCard.IsCfgScaleEnabled
            ? samplerCard.CfgScale
            : e.Builder.Connections.PrimaryCfg ?? throw new ArgumentException("No CFG");

        if (schedulerName == "align_your_steps")
        {
            var samplerSelect = builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSamplerSelect
                {
                    Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.KSamplerSelect)),
                    SamplerName = samplerName
                }
            );

            var alignYourStepsScheduler = builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.AlignYourStepsScheduler
                {
                    Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.AlignYourStepsScheduler)),
                    ModelType = samplerCard.SelectedModelType,
                    Denoise = samplerCard.DenoiseStrength,
                    Steps = samplerCard.Steps
                }
            );

            var hiresCustomSampler = builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.SamplerCustom
                {
                    Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.SamplerCustom)),
                    Model = builder.Connections.GetRefinerOrBaseModel(),
                    AddNoise = true,
                    NoiseSeed = builder.Connections.Seed,
                    Cfg = cfg,
                    Sampler = samplerSelect.Output,
                    Sigmas = alignYourStepsScheduler.Output,
                    Positive = e.Temp.GetRefinerOrBaseConditioning().Positive,
                    Negative = e.Temp.GetRefinerOrBaseConditioning().Negative,
                    LatentImage = builder.GetPrimaryAsLatent(),
                }
            );

            builder.Connections.Primary = hiresCustomSampler.Output1;
        }
        else
        {
            var hiresSampler = builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.KSampler
                {
                    Name = builder.Nodes.GetUniqueName("HiresFix_Sampler"),
                    Model = builder.Connections.GetRefinerOrBaseModel(),
                    Seed = builder.Connections.Seed,
                    Steps = samplerCard.Steps,
                    Cfg = cfg,
                    SamplerName = samplerName ?? throw new ArgumentException("No PrimarySampler"),
                    Scheduler = schedulerName ?? throw new ArgumentException("No PrimaryScheduler"),
                    Positive = e.Temp.GetRefinerOrBaseConditioning().Positive,
                    Negative = e.Temp.GetRefinerOrBaseConditioning().Negative,
                    LatentImage = builder.GetPrimaryAsLatent(),
                    Denoise = samplerCard.DenoiseStrength
                }
            );

            // Set as primary
            builder.Connections.Primary = hiresSampler.Output;
        }

        builder.Connections.PrimarySize = hiresSize;
    }
}
