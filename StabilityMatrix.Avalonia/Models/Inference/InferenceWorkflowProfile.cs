using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Saved Inference projects persist this enum by numeric value, so members are append-only:
/// never insert, reorder, or renumber. Dropdown ordering (Custom last) is handled by
/// <see cref="ViewModels.Inference.ModelCardViewModel.WorkflowProfiles"/>, not member order.
/// </summary>
public enum InferenceWorkflowProfile
{
    [StringValue("Auto")]
    Auto = 0,

    [StringValue("Default / Checkpoint")]
    DefaultCheckpoint = 1,

    [StringValue("Flux")]
    Flux = 2,

    [StringValue("Flux.2")]
    Flux2 = 3,

    [StringValue("Z-Image Base")]
    ZImageBase = 4,

    [StringValue("Z-Image Turbo")]
    ZImageTurbo = 5,

    [StringValue("Anima")]
    Anima = 6,

    [StringValue("HiDream")]
    HiDream = 7,

    [StringValue("Custom")]
    Custom = 8,

    [StringValue("Qwen Image")]
    QwenImage = 9,

    [StringValue("Krea 2 Turbo")]
    Krea2Turbo = 10,

    [StringValue("Krea 2 Raw")]
    Krea2Raw = 11,
}
