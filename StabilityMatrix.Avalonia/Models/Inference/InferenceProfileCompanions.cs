using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// A companion file (text encoder or VAE) an Inference workflow profile expects: how to
/// recognize an installed copy by filename, and the official download to offer when none is
/// installed. Matcher and download live in one record so they can never disagree about what
/// a profile needs.
/// </summary>
public record InferenceCompanion(
    string DisplayName,
    Func<string, bool> MatchesFileName,
    RemoteResource Resource
);

/// <summary>
/// Single owner of the per-profile companion tables. The Model Card's encoder/VAE
/// auto-selection consumes the matchers; the missing-companion download offer consumes the
/// resources. The selected UNET is never part of this — that's the user's choice (including
/// CivitAI fine-tunes); companions are the boring official files every variant of a family
/// shares.
/// </summary>
public static class InferenceProfileCompanions
{
    /// <summary>
    /// Companion text encoders for the profile, in slot order. Empty for profiles where the
    /// encoder is baked into the checkpoint or unknown (DefaultCheckpoint, Custom).
    /// </summary>
    public static IReadOnlyList<InferenceCompanion> GetEncoderCompanions(
        InferenceWorkflowProfile profile,
        HybridModelFile? unetModel
    )
    {
        switch (profile)
        {
            case InferenceWorkflowProfile.Flux2:
                // Klein 4B pairs with qwen_3_4b, Klein 9B with qwen_3_8b — picking the wrong
                // size fails at sampling with a tensor-shape mismatch.
                var encoderSize = unetModel is { } unet
                    ? Flux2KleinModelManager.GetExpectedEncoderSize(unet)
                    : "4b";
                return
                [
                    new InferenceCompanion(
                        encoderSize == "8b" ? "Qwen3 8B text encoder" : "Qwen3 4B text encoder",
                        name =>
                            Flux2KleinModelManager.IsKleinTextEncoder(name)
                            && Flux2KleinModelManager.MatchesEncoderSize(name, encoderSize),
                        encoderSize == "8b"
                            ? RemoteModels.Qwen3_8bTextEncoder
                            : RemoteModels.Qwen3_4bTextEncoder
                    ),
                ];
            case InferenceWorkflowProfile.Flux:
                return
                [
                    new InferenceCompanion(
                        "CLIP-L text encoder",
                        name => name.Contains("clip_l", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.ClipLTextEncoder
                    ),
                    new InferenceCompanion(
                        "T5-XXL text encoder",
                        name =>
                            name.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("t5-xxl", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.T5XxlFp8ScaledTextEncoder
                    ),
                ];
            case InferenceWorkflowProfile.ZImageBase:
            case InferenceWorkflowProfile.ZImageTurbo:
                // Z-Image uses the Qwen3 4B encoder (the same qwen_3_4b.safetensors file
                // Flux.2 Klein 4B uses).
                return
                [
                    new InferenceCompanion(
                        "Qwen3 4B text encoder",
                        name =>
                            Flux2KleinModelManager.IsKleinTextEncoder(name)
                            && Flux2KleinModelManager.MatchesEncoderSize(name, "4b"),
                        RemoteModels.Qwen3_4bTextEncoder
                    ),
                ];
            case InferenceWorkflowProfile.Anima:
                // Official Anima encoder is qwen_3_06b_base.safetensors (Qwen3 0.6B).
                return
                [
                    new InferenceCompanion(
                        "Qwen3 0.6B text encoder",
                        name =>
                            name.Contains("qwen_3_06b", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen3_06b", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen_3_0.6b", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.Qwen3_06bBaseTextEncoder
                    ),
                ];
            case InferenceWorkflowProfile.HiDream:
                return
                [
                    new InferenceCompanion(
                        "CLIP-L text encoder",
                        name => name.Contains("clip_l", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.ClipLHiDreamTextEncoder
                    ),
                    new InferenceCompanion(
                        "CLIP-G text encoder",
                        name => name.Contains("clip_g", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.ClipGHiDreamTextEncoder
                    ),
                    new InferenceCompanion(
                        "T5-XXL text encoder",
                        name =>
                            name.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("t5-xxl", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.T5XxlFp8ScaledTextEncoder
                    ),
                    new InferenceCompanion(
                        "Llama 3.1 8B text encoder",
                        name => name.Contains("llama", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.Llama31_8bInstructTextEncoder
                    ),
                ];
            case InferenceWorkflowProfile.QwenImage:
                // Official encoder is qwen_2.5_vl_7b_fp8_scaled.safetensors (Qwen2.5-VL 7B).
                return
                [
                    new InferenceCompanion(
                        "Qwen2.5-VL 7B text encoder",
                        name =>
                            name.Contains("qwen_2.5_vl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen2.5_vl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen2.5-vl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen_2_5_vl", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.Qwen25Vl7bTextEncoder
                    ),
                ];
            case InferenceWorkflowProfile.Krea2Turbo:
            case InferenceWorkflowProfile.Krea2Raw:
                // Official encoder is qwen3vl_4b_fp8_scaled.safetensors (Qwen3-VL 4B) — a
                // different family from both Klein's text-only qwen_3_4b and Qwen Image's
                // qwen_2.5_vl.
                return
                [
                    new InferenceCompanion(
                        "Qwen3-VL 4B text encoder",
                        name =>
                            name.Contains("qwen3vl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen3-vl", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen3_vl", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.Qwen3Vl4bTextEncoder
                    ),
                ];
            default:
                return [];
        }
    }

    /// <summary>
    /// The companion VAE for the profile, or null where the checkpoint bundles its own.
    /// Flux.1, Z-Image and HiDream all use the Flux.1 VAE (ae.safetensors); Flux.2 has its
    /// own VAE; Anima, Qwen Image and Krea 2 all reuse the Qwen Image VAE.
    /// </summary>
    public static InferenceCompanion? GetVaeCompanion(InferenceWorkflowProfile profile) =>
        profile switch
        {
            InferenceWorkflowProfile.Flux2 => new InferenceCompanion(
                "Flux.2 VAE",
                Flux2KleinModelManager.IsFlux2Vae,
                RemoteModels.Flux2Vae
            ),
            InferenceWorkflowProfile.Flux
            or InferenceWorkflowProfile.ZImageBase
            or InferenceWorkflowProfile.ZImageTurbo
            or InferenceWorkflowProfile.HiDream => new InferenceCompanion(
                "Flux VAE (ae.safetensors)",
                name => name.Equals("ae.safetensors", StringComparison.OrdinalIgnoreCase),
                RemoteModels.FluxVae
            ),
            InferenceWorkflowProfile.Anima
            or InferenceWorkflowProfile.QwenImage
            or InferenceWorkflowProfile.Krea2Turbo
            or InferenceWorkflowProfile.Krea2Raw => new InferenceCompanion(
                "Qwen Image VAE",
                name => name.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase),
                RemoteModels.QwenImageVae
            ),
            _ => null,
        };
}
