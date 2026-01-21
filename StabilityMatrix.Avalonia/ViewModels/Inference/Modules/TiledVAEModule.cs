using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using NLog;


namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<TiledVAEModule>]
public class TiledVAEModule : ModuleBase
{
    public TiledVAEModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        this.logger = logger;
        Title = "Tiled VAE Decode";
        AddCards(vmFactory.Get<TiledVAECardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<TiledVAECardViewModel>();

        e.PreOutputActions.Add(args =>
        {
            var builder = args.Builder;

            // Only apply if primary is in latent space
            if (builder.Connections.Primary?.IsT0 != true)
                return;

            var latent = builder.Connections.Primary.AsT0;
            var vae = builder.Connections.GetDefaultVAE();

            logger.LogDebug("TiledVAE: Injecting TiledVAEDecode");
            logger.LogDebug(
                "UseCustomTemporalTiling value at runtime: {value}",
                card.UseCustomTemporalTiling
            );

            var node = builder.Nodes.AddTypedNode(
                new ComfyNodeBuilder.TiledVAEDecode
                {
                    Name = builder.Nodes.GetUniqueName("TiledVAEDecode"),
                    Samples = latent,
                    Vae = vae,
                    TileSize = card.TileSize,
                    Overlap = card.Overlap,

                    // Temporal tiling (WAN requires temporal tiling)
                    TemporalSize = card.UseCustomTemporalTiling ? card.TemporalSize : 64,
                    TemporalOverlap = card.UseCustomTemporalTiling ? card.TemporalOverlap : 8,
                }
            );

            // Update primary connection to the decoded image
            builder.Connections.Primary = node.Output;
        });
    }
}
