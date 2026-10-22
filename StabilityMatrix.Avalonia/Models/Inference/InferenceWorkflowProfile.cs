using System.Text.Json.Serialization;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// A model-family workflow profile: the single owner of every per-family fact Inference
/// needs (clip type, loader style, latent, sampling defaults, companion files, CivitAI
/// tags). Adding a model family means declaring one new instance in
/// <see cref="InferenceWorkflowProfiles"/> — no switch statements elsewhere.
/// Persisted in saved projects by <see cref="Key"/> (see
/// <see cref="InferenceWorkflowProfileJsonConverter"/>); <see cref="LegacyValue"/> is the
/// numeric value pre-2.17 projects stored and exists only for reading them.
/// Instances are singletons — compare by reference. This file is the contract half of the
/// partial class; the profile instances and detection live in
/// InferenceWorkflowProfile.Profiles.cs.
/// </summary>
[JsonConverter(typeof(InferenceWorkflowProfileJsonConverter))]
public sealed partial class InferenceWorkflowProfile
{
    /// <summary>Stable persisted identity — never rename.</summary>
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Numeric value pre-2.17 saved projects stored for this profile; null for profiles that
    /// were only ever persisted by key.
    /// </summary>
    public int? LegacyValue { get; init; }

    /// <summary>ComfyUI CLIPLoader type; null leaves the user's selection alone.</summary>
    public string? ClipType { get; init; }

    /// <summary>
    /// Whether the profile loads a standalone diffusion model + separate encoders/VAE (true)
    /// or an all-in-one checkpoint (false). Null = follow the model's folder / user's choice
    /// (Auto, Custom).
    /// </summary>
    public bool? UsesStandaloneLoader { get; init; }

    /// <summary>
    /// Whether the model file must physically live in DiffusionModels (true), the checkpoint
    /// folder (false), or either (null — Flux.1 ships both packagings). Drives the
    /// misplaced-file warning.
    /// </summary>
    public bool? RequiresStandaloneFile { get; init; }

    public LatentType Latent { get; init; } = LatentType.Default;

    /// <summary>Sampling via CFGGuider + Flux2Scheduler instead of a plain KSampler.</summary>
    public bool UsesFlux2Scheduler { get; init; }

    /// <summary>Sampling via the FluxGuidance custom-sampler path (Flux.1, HiDream).</summary>
    public bool UsesFluxGuidance { get; init; }

    /// <summary>Applies ModelSamplingAuraFlow with the card's Shift value (Z-Image, Qwen).</summary>
    public bool UsesAuraFlowShift { get; init; }

    /// <summary>Applies ModelSamplingSD3 with the card's Shift value (HiDream).</summary>
    public bool UsesSd3ModelSamplingShift { get; init; }

    public RecommendedSamplerDefaults? RecommendedDefaults { get; init; }

    /// <summary>CivitAI base-model tags the model picker pre-filters to for this profile.</summary>
    public IReadOnlyList<string> PreferredBaseModels { get; init; } = [];

    /// <summary>
    /// Companion text encoders in slot order. A factory because Flux.2 sizes its encoder
    /// (qwen_3_4b vs qwen_3_8b) to the selected UNET.
    /// </summary>
    public Func<HybridModelFile?, IReadOnlyList<InferenceCompanion>> GetEncoderCompanions { get; init; } =
        _ => [];

    public InferenceCompanion? VaeCompanion { get; init; }

    /// <summary>Whether the profile shows the Shift control.</summary>
    public bool UsesShift => UsesAuraFlowShift || UsesSd3ModelSamplingShift;

    /// <summary>
    /// The profile whose latent, sampler path and shift node a workflow follows: this profile,
    /// or for <see cref="Custom"/> the built-in profile that owns <paramref name="clipType"/>.
    /// </summary>
    public InferenceWorkflowProfile ForSampling(string? clipType) =>
        this == Custom ? ForClipType(clipType) ?? Custom : this;

    public override string ToString() => DisplayName;
}

/// <summary>
/// One-click sampler defaults for a profile. <paramref name="DescriptionOverride"/> exists
/// for profiles whose generation-time scheduler differs from the stored one (Flux.2 swaps in
/// Flux2Scheduler).
/// </summary>
public record RecommendedSamplerDefaults(
    ComfySampler Sampler,
    ComfyScheduler Scheduler,
    int Steps,
    double CfgScale,
    double? Shift = null,
    string? DescriptionOverride = null
)
{
    public string Description =>
        DescriptionOverride
        ?? $"{Sampler.DisplayName} / {Scheduler.DisplayName} / {Steps} steps / CFG {CfgScale}"
            + (Shift is { } shift ? $" / Shift {shift}" : string.Empty);
}
