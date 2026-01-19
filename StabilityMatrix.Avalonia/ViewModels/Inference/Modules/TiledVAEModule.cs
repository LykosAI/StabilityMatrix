using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<TiledVAEModule>]
public class TiledVAEModule : ModuleBase
{
    public TiledVAEModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Tiled VAE Decode";
        AddCards(vmFactory.Get<TiledVAECardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<TiledVAECardViewModel>();

        // Register a pre-output action that replaces standard VAE decode with tiled decode
        e.PreOutputActions.Add(args =>
        {
            var builder = args.Builder;

            // Only apply if primary is in latent space
            if (builder.Connections.Primary?.IsT0 != true)
                return;

            var latent = builder.Connections.Primary.AsT0;
            var vae = builder.Connections.GetDefaultVAE();

            logger.LogWarning("TiledVAE: Injecting TiledVAEDecode");
            logger.LogWarning("UseCustomTemporalTiling value at runtime: {Value}", card.UseCustomTemporalTiling);

            // Always valid values (Wan requires temporal tiling)
            int temporalSize = card.UseCustomTemporalTiling ? card.TemporalSize : 64;
            int temporalOverlap = card.UseCustomTemporalTiling ? card.TemporalOverlap : 8;

            var node = builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.TiledVAEDecode
                {
                    Name = builder.Nodes.GetUniqueName("TiledVAEDecode"),
                    Samples = latent,
                    Vae = vae,
                    TileSize = card.TileSize,
                    Overlap = card.Overlap,
                    TemporalSize = card.TemporalSize,
                    TemporalOverlap = card.TemporalOverlap
                }
            );

            // Update primary connection to the decoded image
            builder.Connections.Primary = tiledDecode.Output;
        });
    }
}
