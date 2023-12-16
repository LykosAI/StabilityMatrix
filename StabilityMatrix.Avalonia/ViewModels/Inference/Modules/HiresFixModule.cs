using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Controls;
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
[Transient]
public partial class HiresFixModule : ModuleBase
{
    /// <inheritdoc />
    public override bool IsSettingsEnabled => true;

    /// <inheritdoc />
    public override IRelayCommand SettingsCommand => OpenSettingsDialogCommand;

    /// <inheritdoc />
    public HiresFixModule(ServiceManager<ViewModelBase> vmFactory)
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
                builder.Connections.Primary ?? throw new ArgumentException("No Primary"),
                builder.Connections.GetDefaultVAE(),
                selectedUpscaler,
                hiresSize.Width,
                hiresSize.Height
            );
        }

        var hiresSampler = builder
            .Nodes
            .AddTypedNode(
                new ComfyNodeBuilder.KSampler
                {
                    Name = builder.Nodes.GetUniqueName("HiresFix_Sampler"),
                    Model = builder.Connections.GetRefinerOrBaseModel(),
                    Seed = builder.Connections.Seed,
                    Steps = samplerCard.Steps,
                    Cfg = samplerCard.CfgScale,
                    SamplerName =
                        samplerCard.SelectedSampler?.Name
                        ?? e.Builder.Connections.PrimarySampler?.Name
                        ?? throw new ArgumentException("No PrimarySampler"),
                    Scheduler =
                        samplerCard.SelectedScheduler?.Name
                        ?? e.Builder.Connections.PrimaryScheduler?.Name
                        ?? throw new ArgumentException("No PrimaryScheduler"),
                    Positive = builder.Connections.GetRefinerOrBaseConditioning(),
                    Negative = builder.Connections.GetRefinerOrBaseNegativeConditioning(),
                    LatentImage = builder.GetPrimaryAsLatent(),
                    Denoise = samplerCard.DenoiseStrength
                }
            );

        // Set as primary
        builder.Connections.Primary = hiresSampler.Output;
        builder.Connections.PrimarySize = hiresSize;
    }
}
