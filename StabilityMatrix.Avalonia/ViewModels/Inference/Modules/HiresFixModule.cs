using System.ComponentModel.DataAnnotations;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[Transient]
public class HiresFixModule : ModuleBase
{
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
                "HiresFix",
                builder.Connections.Primary!,
                builder.Connections.PrimaryVAE!,
                selectedUpscaler,
                hiresSize.Width,
                hiresSize.Height
            );
        }

        // Use refiner model if set, or base if not
        var hiresSampler = builder.Nodes.AddNamedNode(
            ComfyNodeBuilder.KSampler(
                builder.Nodes.GetUniqueName("HiresFix_Sampler"),
                builder.Connections.GetRefinerOrBaseModel(),
                builder.Connections.Seed,
                samplerCard.Steps,
                samplerCard.CfgScale,
                // Use hires sampler name if not null, otherwise use the normal sampler
                samplerCard.SelectedSampler
                    ?? samplerCard.SelectedSampler
                    ?? throw new ValidationException("Sampler not selected"),
                samplerCard.SelectedScheduler
                    ?? samplerCard.SelectedScheduler
                    ?? throw new ValidationException("Scheduler not selected"),
                builder.Connections.GetRefinerOrBaseConditioning(),
                builder.Connections.GetRefinerOrBaseNegativeConditioning(),
                builder.GetPrimaryAsLatent(),
                samplerCard.DenoiseStrength
            )
        );

        // Set as primary
        builder.Connections.Primary = hiresSampler.Output;
        builder.Connections.PrimarySize = hiresSize;
    }
}
