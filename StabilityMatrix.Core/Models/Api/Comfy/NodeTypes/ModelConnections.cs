namespace StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

/// <summary>
/// Connections from a loaded model
/// </summary>
public record ModelConnections(string Name)
{
    public ModelConnections(ModelConnections other)
    {
        Name = other.Name;
        Model = other.Model;
        VAE = other.VAE;
        Clip = other.Clip;
        Conditioning = other.Conditioning;
        ClipVision = other.ClipVision;
    }

    public ModelNodeConnection? Model { get; set; }

    public VAENodeConnection? VAE { get; set; }

    public ClipNodeConnection? Clip { get; set; }

    public ConditioningConnections? Conditioning { get; set; }

    public ClipVisionNodeConnection? ClipVision { get; set; }
}
