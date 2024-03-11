using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Core.Models.Inference;

public class ModuleApplyStepTemporaryArgs
{
    /// <summary>
    /// Temporary Primary apply step, used by ControlNet ReferenceOnly which changes the latent.
    /// </summary>
    public PrimaryNodeConnection? Primary { get; set; }

    public VAENodeConnection? PrimaryVAE { get; set; }

    /// <summary>
    /// Used by Reference-Only ControlNet to indicate that <see cref="Primary"/> has been batched.
    /// </summary>
    public bool IsPrimaryTempBatched { get; set; }

    /// <summary>
    /// When <see cref="IsPrimaryTempBatched"/> is true, this is the index of the temp batch to pick after sampling.
    /// </summary>
    public int PrimaryTempBatchPickIndex { get; set; }

    public Dictionary<string, ModelConnections> Models { get; set; } =
        new() { ["Base"] = new ModelConnections("Base"), ["Refiner"] = new ModelConnections("Refiner") };

    public ModelConnections Base => Models["Base"];
    public ModelConnections Refiner => Models["Refiner"];

    public ConditioningConnections GetRefinerOrBaseConditioning()
    {
        return Refiner.Conditioning
            ?? Base.Conditioning
            ?? throw new NullReferenceException("No Refiner or Base Conditioning");
    }

    public ModelNodeConnection GetRefinerOrBaseModel()
    {
        return Refiner.Model ?? Base.Model ?? throw new NullReferenceException("No Refiner or Base Model");
    }

    public VAENodeConnection GetDefaultVAE()
    {
        return PrimaryVAE ?? Refiner.VAE ?? Base.VAE ?? throw new NullReferenceException("No VAE");
    }
}
