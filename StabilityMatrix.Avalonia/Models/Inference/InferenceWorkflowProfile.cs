using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Persisted in saved Inference projects as the member name (see
/// <see cref="InferenceWorkflowProfileJsonConverter"/>); the explicit numeric values remain
/// the read-compatibility contract for projects saved by pre-2.17 builds, so they are still
/// append-only. Dropdown ordering (Custom last) is handled by
/// <see cref="ViewModels.Inference.ModelCardViewModel.WorkflowProfiles"/>, not member order.
/// </summary>
[JsonConverter(typeof(InferenceWorkflowProfileJsonConverter))]
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
