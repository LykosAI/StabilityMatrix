namespace StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

/// <summary>
/// Connections from a loaded model
/// </summary>
public record ModelConnections(string Name)
{
    public ModelNodeConnection? Model { get; set; }

    public VAENodeConnection? VAE { get; set; }

    public ClipNodeConnection? Clip { get; set; }

    public ConditioningConnections? Conditioning { get; set; }
}
