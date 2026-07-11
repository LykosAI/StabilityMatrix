using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Models.Inference;

public enum InferenceWorkflowProfile
{
    [StringValue("Auto")]
    Auto,

    [StringValue("Default / Checkpoint")]
    DefaultCheckpoint,

    [StringValue("Flux")]
    Flux,

    [StringValue("Flux.2")]
    Flux2,

    [StringValue("Z-Image Base")]
    ZImageBase,

    [StringValue("Z-Image Turbo")]
    ZImageTurbo,

    [StringValue("Anima")]
    Anima,

    [StringValue("HiDream")]
    HiDream,

    [StringValue("Custom")]
    Custom,
}
