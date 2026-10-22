using System.Text.RegularExpressions;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// The built-in workflow profiles and the model-to-profile detection logic — the data half
/// of the partial class (the contract half lives in InferenceWorkflowProfile.cs).
/// Declaration here is the whole act of adding a model family; display order comes from
/// <see cref="All"/> (Custom deliberately last), free of the legacy numeric values.
/// </summary>
public sealed partial class InferenceWorkflowProfile
{
    // Shared companion instances — several profiles pair with the same official files.
    // Declared BEFORE the profiles: static members initialize in textual order, so a profile
    // referencing one of these from below would capture null.
    private static InferenceCompanion FluxVaeCompanion { get; } =
        new(
            "Flux VAE (ae.safetensors)",
            name => name.Equals("ae.safetensors", StringComparison.OrdinalIgnoreCase),
            RemoteModels.FluxVae
        );

    private static InferenceCompanion QwenVaeCompanion { get; } =
        new(
            "Qwen Image VAE",
            name => name.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase),
            RemoteModels.QwenImageVae
        );

    private static InferenceCompanion Flux2VaeCompanion { get; } =
        new("Flux.2 VAE", Flux2KleinModelManager.IsFlux2Vae, RemoteModels.Flux2Vae);

    // Z-Image uses the Qwen3 4B encoder (the same qwen_3_4b.safetensors file Klein 4B uses).
    private static InferenceCompanion ZImageEncoderCompanion { get; } =
        new(
            "Qwen3 4B text encoder",
            name =>
                Flux2KleinModelManager.IsKleinTextEncoder(name)
                && Flux2KleinModelManager.MatchesEncoderSize(name, "4b"),
            RemoteModels.Qwen3_4bTextEncoder
        );

    // Krea 2's encoder is Qwen3-VL 4B (qwen3vl_4b_fp8_scaled) — a different family from both
    // Klein's text-only qwen_3_4b and Qwen Image's qwen_2.5_vl_7b.
    private static InferenceCompanion Krea2EncoderCompanion { get; } =
        new(
            "Qwen3-VL 4B text encoder",
            name =>
                System.Text.RegularExpressions.Regex.IsMatch(
                    name,
                    @"qwen[_ .-]*3[_ .-]*vl[_ .-]*4b(?:[_ .-]|$)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                ) && !name.Contains("minimax", StringComparison.OrdinalIgnoreCase),
            RemoteModels.Qwen3Vl4bTextEncoder
        );

    public static InferenceWorkflowProfile Auto { get; } =
        new()
        {
            Key = "Auto",
            DisplayName = "Auto",
            LegacyValue = 0,
        };

    public static InferenceWorkflowProfile DefaultCheckpoint { get; } =
        new()
        {
            Key = "DefaultCheckpoint",
            DisplayName = "Default / Checkpoint",
            LegacyValue = 1,
            UsesStandaloneLoader = false,
            RequiresStandaloneFile = false,
            RecommendedDefaults = new(ComfySampler.EulerAncestral, ComfyScheduler.Normal, 30, 5),
        };

    public static InferenceWorkflowProfile Flux { get; } =
        new()
        {
            Key = "Flux",
            DisplayName = "Flux",
            LegacyValue = 2,
            ClipType = "flux",
            UsesStandaloneLoader = true,
            // Flux.1 is packaging-flexible: split UNETs in DiffusionModels and all-in-one
            // checkpoints are both common and valid.
            RequiresStandaloneFile = null,
            Latent = LatentType.Sd3,
            UsesFluxGuidance = true,
            RecommendedDefaults = new(ComfySampler.Euler, ComfyScheduler.Simple, 20, 3.5),
            PreferredBaseModels = ["Flux.1"],
            GetEncoderCompanions = _ =>
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
                ],
            VaeCompanion = FluxVaeCompanion,
        };

    public static InferenceWorkflowProfile Flux2 { get; } =
        new()
        {
            Key = "Flux2",
            DisplayName = "Flux.2",
            LegacyValue = 3,
            ClipType = "flux2",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Flux2,
            UsesFlux2Scheduler = true,
            RecommendedDefaults = new(
                ComfySampler.Euler,
                ComfyScheduler.Normal,
                20,
                5,
                DescriptionOverride: "Euler / Flux2Scheduler / 20 steps / CFG 5"
            ),
            PreferredBaseModels = ["Flux.2"],
            // Klein 4B pairs with qwen_3_4b, Klein 9B with qwen_3_8b — picking the wrong
            // size fails at sampling with a tensor-shape mismatch.
            GetEncoderCompanions = unetModel =>
            {
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
            },
            VaeCompanion = Flux2VaeCompanion,
        };

    public static InferenceWorkflowProfile ZImageBase { get; } =
        new()
        {
            Key = "ZImageBase",
            DisplayName = "Z-Image Base",
            LegacyValue = 4,
            ClipType = "lumina2",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Sd3,
            UsesAuraFlowShift = true,
            RecommendedDefaults = new(ComfySampler.ResMultistep, ComfyScheduler.Simple, 30, 4),
            PreferredBaseModels = ["ZImageBase"],
            GetEncoderCompanions = _ => [ZImageEncoderCompanion],
            VaeCompanion = FluxVaeCompanion,
        };

    public static InferenceWorkflowProfile ZImageTurbo { get; } =
        new()
        {
            Key = "ZImageTurbo",
            DisplayName = "Z-Image Turbo",
            LegacyValue = 5,
            ClipType = "lumina2",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Sd3,
            UsesAuraFlowShift = true,
            RecommendedDefaults = new(ComfySampler.ResMultistep, ComfyScheduler.Simple, 8, 1),
            PreferredBaseModels = ["ZImageTurbo"],
            GetEncoderCompanions = _ => [ZImageEncoderCompanion],
            VaeCompanion = FluxVaeCompanion,
        };

    public static InferenceWorkflowProfile Anima { get; } =
        new()
        {
            Key = "Anima",
            DisplayName = "Anima",
            LegacyValue = 6,
            ClipType = "stable_diffusion",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            RecommendedDefaults = new(ComfySampler.ErSde, ComfyScheduler.Simple, 30, 4),
            PreferredBaseModels = ["Anima"],
            // Official Anima encoder is qwen_3_06b_base.safetensors (Qwen3 0.6B).
            GetEncoderCompanions = _ =>
                [
                    new InferenceCompanion(
                        "Qwen3 0.6B text encoder",
                        name =>
                            name.Contains("qwen_3_06b", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen3_06b", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("qwen_3_0.6b", StringComparison.OrdinalIgnoreCase),
                        RemoteModels.Qwen3_06bBaseTextEncoder
                    ),
                ],
            VaeCompanion = QwenVaeCompanion,
        };

    public static InferenceWorkflowProfile HiDream { get; } =
        new()
        {
            Key = "HiDream",
            DisplayName = "HiDream",
            LegacyValue = 7,
            ClipType = "hidream",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Sd3,
            UsesFluxGuidance = true,
            UsesSd3ModelSamplingShift = true,
            PreferredBaseModels = ["HiDream"],
            GetEncoderCompanions = _ =>
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
                ],
            VaeCompanion = FluxVaeCompanion,
        };

    // Custom takes its sampling from the built-in profile that owns the selected clip type
    // (see ForSampling). These flags cover clip types no built-in profile owns (sd3, chroma,
    // wan, ...).
    public static InferenceWorkflowProfile Custom { get; } =
        new()
        {
            Key = "Custom",
            DisplayName = "Custom",
            LegacyValue = 8,
            Latent = LatentType.Sd3,
            UsesFluxGuidance = true,
        };

    public static InferenceWorkflowProfile QwenImage { get; } =
        new()
        {
            Key = "QwenImage",
            DisplayName = "Qwen Image",
            LegacyValue = 9,
            ClipType = "qwen_image",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Sd3,
            UsesAuraFlowShift = true,
            RecommendedDefaults = new(ComfySampler.Euler, ComfyScheduler.Simple, 50, 4, Shift: 3.1),
            PreferredBaseModels = ["Qwen", "Qwen 2"],
            // Official encoder is qwen_2.5_vl_7b_fp8_scaled.safetensors (Qwen2.5-VL 7B).
            GetEncoderCompanions = _ =>
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
                ],
            VaeCompanion = QwenVaeCompanion,
        };

    public static InferenceWorkflowProfile Krea2Turbo { get; } =
        new()
        {
            Key = "Krea2Turbo",
            DisplayName = "Krea 2 Turbo",
            LegacyValue = 10,
            ClipType = "krea2",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Sd3,
            RecommendedDefaults = new(ComfySampler.Euler, ComfyScheduler.Simple, 8, 1),
            GetEncoderCompanions = _ => [Krea2EncoderCompanion],
            VaeCompanion = QwenVaeCompanion,
        };

    public static InferenceWorkflowProfile Krea2Raw { get; } =
        new()
        {
            Key = "Krea2Raw",
            DisplayName = "Krea 2 Raw",
            LegacyValue = 11,
            ClipType = "krea2",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Sd3,
            // No recommended defaults: Krea hasn't published official Raw sampler settings.
            GetEncoderCompanions = _ => [Krea2EncoderCompanion],
            VaeCompanion = QwenVaeCompanion,
        };

    // Base and SFT share one graph and the same settings. Shift 5 is ComfyUI's model default,
    // so no model-sampling node (and no Shift control) is needed.
    public static InferenceWorkflowProfile MossImage { get; } =
        new()
        {
            Key = "MossImage",
            DisplayName = "MOSS Image",
            ClipType = "moss_image",
            UsesStandaloneLoader = true,
            RequiresStandaloneFile = true,
            Latent = LatentType.Flux2,
            RecommendedDefaults = new(ComfySampler.ErSde, ComfyScheduler.Simple, 25, 2.5),
            PreferredBaseModels = ["MOSS Image"],
            GetEncoderCompanions = _ =>
                [
                    new InferenceCompanion(
                        "Qwen3.5 9B text encoder",
                        name => Qwen35_9bEncoderRegex().IsMatch(name),
                        RemoteModels.Qwen35_9bTextEncoder
                    ),
                ],
            // MOSS shares Flux.2's latent space; its own small-decoder VAE build matches too.
            VaeCompanion = Flux2VaeCompanion,
        };

    /// <summary>Display order for the profile dropdown — Custom deliberately last.</summary>
    public static IReadOnlyList<InferenceWorkflowProfile> All { get; } =
        [
            Auto,
            DefaultCheckpoint,
            Flux,
            Flux2,
            ZImageBase,
            ZImageTurbo,
            Anima,
            HiDream,
            QwenImage,
            Krea2Turbo,
            Krea2Raw,
            MossImage,
            Custom,
        ];

    public static InferenceWorkflowProfile FromKey(string? key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Auto;

    public static InferenceWorkflowProfile FromLegacyValue(int value) =>
        All.FirstOrDefault(p => p.LegacyValue == value) ?? Auto;

    /// <summary>
    /// The built-in profile that owns a ComfyUI clip type, or null when none does. Profiles that
    /// share a clip type (Z-Image Base and Turbo, Krea 2 Turbo and Raw) sample the same way, so
    /// the first match stands for all of them.
    /// </summary>
    public static InferenceWorkflowProfile? ForClipType(string? clipType) =>
        clipType is null ? null : All.FirstOrDefault(p => p.ClipType == clipType);

    /// <summary>
    /// Detects the workflow profile for a model. CivitAI BaseModel tags win over filename
    /// heuristics; check order matters ("Flux.1 Krea" must hit Flux before the Krea checks,
    /// "flux2" before "flux").
    /// </summary>
    public static InferenceWorkflowProfile Detect(HybridModelFile? model, bool isUnetModel)
    {
        if (!isUnetModel)
            return DefaultCheckpoint;

        var baseModel = model?.Local?.ConnectedModelInfo?.BaseModel;

        if (!string.IsNullOrWhiteSpace(baseModel))
        {
            if (baseModel.Equals("ZImageTurbo", StringComparison.OrdinalIgnoreCase))
                return ZImageTurbo;

            if (baseModel.Equals("ZImageBase", StringComparison.OrdinalIgnoreCase))
                return ZImageBase;

            if (baseModel.Equals("Anima", StringComparison.OrdinalIgnoreCase))
                return Anima;

            if (baseModel.StartsWith("Flux.2", StringComparison.OrdinalIgnoreCase))
                return Flux2;

            if (baseModel.StartsWith("Flux.1", StringComparison.OrdinalIgnoreCase))
                return Flux;

            if (baseModel.Equals("HiDream", StringComparison.OrdinalIgnoreCase))
                return HiDream;

            // Covers "Qwen" and "Qwen 2"; must stay after the Flux checks so "Flux.1 Krea"
            // never reaches the Krea check below.
            if (baseModel.StartsWith("Qwen", StringComparison.OrdinalIgnoreCase))
                return QwenImage;

            if (baseModel.StartsWith("Krea", StringComparison.OrdinalIgnoreCase))
            {
                return (model?.RelativePath ?? string.Empty).Contains(
                    "raw",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? Krea2Raw
                    : Krea2Turbo;
            }

            if (baseModel.StartsWith("MOSS", StringComparison.OrdinalIgnoreCase))
                return MossImage;
        }

        var name = model?.RelativePath ?? string.Empty;

        if (
            name.Contains("z_image", StringComparison.OrdinalIgnoreCase)
            || name.Contains("z-image", StringComparison.OrdinalIgnoreCase)
            || name.Contains("zimage", StringComparison.OrdinalIgnoreCase)
        )
        {
            return name.Contains("turbo", StringComparison.OrdinalIgnoreCase) ? ZImageTurbo : ZImageBase;
        }

        if (
            name.Contains("flux2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("flux-2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("flux_2", StringComparison.OrdinalIgnoreCase)
        )
            return Flux2;

        if (name.Contains("flux", StringComparison.OrdinalIgnoreCase))
            return Flux;

        if (IsAnimaName(name))
            return Anima;

        if (name.Contains("hidream", StringComparison.OrdinalIgnoreCase))
            return HiDream;

        // "qwen" alone is too broad (qwen_3_4b encoders, Anima pairings live in other
        // folders but share the token); require the qwen-image compound. Edit-variant
        // UNets (qwen_image_edit_*) also land here — they load and sample fine as t2i.
        if (
            name.Contains("qwen_image", StringComparison.OrdinalIgnoreCase)
            || name.Contains("qwen-image", StringComparison.OrdinalIgnoreCase)
            || name.Contains("qwenimage", StringComparison.OrdinalIgnoreCase)
        )
            return QwenImage;

        // "krea2" specifically, not bare "krea": Flux.1 Krea files ("flux-1-krea-dev")
        // are Flux models and are already caught by the flux checks above.
        if (
            name.Contains("krea2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("krea-2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("krea_2", StringComparison.OrdinalIgnoreCase)
        )
        {
            return name.Contains("raw", StringComparison.OrdinalIgnoreCase) ? Krea2Raw : Krea2Turbo;
        }

        if (MossImageNameRegex().IsMatch(name))
            return MossImage;

        return DefaultCheckpoint;
    }

    /// <summary>
    /// Whether a CivitAI BaseModel tag is specific enough to override filename heuristics for
    /// a checkpoint-folder file. "Other" remains inconclusive, while known UNet-only tags are
    /// allowed to proceed to the misplaced-model check.
    /// </summary>
    public static bool IsKnownCheckpointBaseModelTag(string baseModel) =>
        Enum.GetValues<CivitBaseModelType>()
            .Any(type =>
                type is not (CivitBaseModelType.Other or CivitBaseModelType.HiDream)
                && type.GetStringValue().Equals(baseModel, StringComparison.OrdinalIgnoreCase)
            );

    /// <summary>
    /// Matches "anima" only as its own token — "animagine" / "animaPencil" are SDXL models
    /// that must not be detected as the Anima architecture.
    /// </summary>
    private static bool IsAnimaName(string name) => AnimaNameRegex().IsMatch(name);

    [GeneratedRegex(@"(?<![\p{L}])anima(?![\p{L}])", RegexOptions.IgnoreCase)]
    private static partial Regex AnimaNameRegex();

    /// <summary>"moss_image" / "moss-image" / "mossimage", the way OpenMOSS and Comfy-Org name the files.</summary>
    [GeneratedRegex(@"moss[_ .-]?image", RegexOptions.IgnoreCase)]
    private static partial Regex MossImageNameRegex();

    /// <summary>
    /// Qwen3.5 9B ("qwen3.5_9b_w4a8_gptq", "qwen3_5_9b_bf16"). Not Klein's qwen_3_8b or any 4B,
    /// which are shape-incompatible.
    /// </summary>
    [GeneratedRegex(@"qwen[_ .-]*3[_ .-]*5[_ .-]*9b(?:[_ .-]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex Qwen35_9bEncoderRegex();
}
