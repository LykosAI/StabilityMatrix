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
public class FluxHiresFixModule(ServiceManager<ViewModelBase> vmFactory) : HiresFixModule(vmFactory)
{
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

        var hiresSampler = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.SamplerCustomAdvanced
            {
                Name = builder.Nodes.GetUniqueName("HiresFix_Sampler"),
                Guider = builder.Connections.PrimaryGuider,
                Noise = builder.Connections.PrimaryNoise,
                Sampler = builder.Connections.PrimarySamplerNode,
                Sigmas = builder.Connections.PrimarySigmas,
                LatentImage = builder.GetPrimaryAsLatent()
            }
        );

        // Set as primary
        builder.Connections.Primary = hiresSampler.Output1;
        builder.Connections.PrimarySize = hiresSize;
    }
}
