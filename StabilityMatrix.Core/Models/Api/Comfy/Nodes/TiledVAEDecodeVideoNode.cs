namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

public record TiledVAEDecodeVideoNode : ComfyNode
{
    public override string ClassType => "TiledVAEDecodeVideo";

    public record Inputs(
        ComfyNodeConnection Vae,
        ComfyNodeConnection VideoLatent,
        int TileSize,
        int Overlap
    );

    public Inputs Input { get; init; } = new(
        Vae: new(),
        VideoLatent: new(),
        TileSize: 256,
        Overlap: 32
    );
}
