using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<CfzCudnnToggleModule>]
public class CfzCudnnToggleModule : ModuleBase
{
    /// <inheritdoc />
    public CfzCudnnToggleModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "CUDNN Toggle (ComfyUI-Zluda)";
        AddCards(vmFactory.Get<CfzCudnnToggleCardViewModel>());
    }

    /// <summary>
    /// Applies CUDNN Toggle node between sampler latent output and VAE decode
    /// This prevents "GET was unable to find an engine" errors on AMD cards with Zluda
    /// </summary>
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        // Get the primary connection (can be latent or image)
        var primary = e.Builder.Connections.Primary;
        if (primary == null)
        {
            return; // No primary connection to process
        }

        // Check if primary is a latent (from sampler output)
        if (primary.IsT0) // T0 is LatentNodeConnection
        {
            var card = GetCard<CfzCudnnToggleCardViewModel>();
            var latentConnection = primary.AsT0;

            // Insert CUDNN toggle node between sampler and VAE decode
            var cudnnToggleOutput = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.CUDNNToggleAutoPassthrough
                {
                    Name = e.Nodes.GetUniqueName("CUDNNToggle"),
                    Model = null,
                    Conditioning = null,
                    Latent = latentConnection, // Pass through the latent from sampler
                    EnableCudnn = !card.DisableCudnn,
                    CudnnBenchmark = false,
                }
            );

            // Update the primary connection to use the CUDNN toggle latent output (Output3)
            // This ensures VAE decode receives latent from CUDNN toggle instead of directly from sampler
            e.Builder.Connections.Primary = cudnnToggleOutput.Output3;
        }
    }
}
