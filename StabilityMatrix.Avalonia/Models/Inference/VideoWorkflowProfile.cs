using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// A model-family workflow profile for the generic video tabs: the single owner of every
/// per-family fact video Inference needs (clip type, which family-specific controls to show).
/// Adding a video model family means declaring one new instance here — no switch statements
/// elsewhere. The video counterpart of <see cref="InferenceWorkflowProfile"/>.
/// Persisted in saved projects by <see cref="Key"/> (see
/// <see cref="VideoWorkflowProfileJsonConverter"/>). Instances are singletons — compare by
/// reference.
/// </summary>
[JsonConverter(typeof(VideoWorkflowProfileJsonConverter))]
public sealed class VideoWorkflowProfile
{
    /// <summary>Stable persisted identity — never rename.</summary>
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>ComfyUI CLIPLoader type for the family's text encoder. Null only on Auto.</summary>
    public string? ClipType { get; init; }

    /// <summary>Applies ModelSamplingSD3 with the card's Shift value, and shows the Shift control.</summary>
    public bool UsesModelSamplingShift { get; init; }

    /// <summary>Offers the optional high/low-noise dual-expert pair (Wan 2.2 14B MoE).</summary>
    public bool SupportsMoeExperts { get; init; }

    /// <summary>Shows the empty-latent override submenu (Wan 2.2 5B vs 14B / 2.1).</summary>
    public bool ShowsLatentSourceMode { get; init; }

    public override string ToString() => DisplayName;

    /// <summary>Resolves to a concrete profile from the selected model (see <see cref="Detect"/>).</summary>
    public static VideoWorkflowProfile Auto { get; } = new() { Key = "Auto", DisplayName = "Auto" };

    /// <summary>Wan 2.1 / 2.2 (all variants).</summary>
    public static VideoWorkflowProfile Wan { get; } =
        new()
        {
            Key = "Wan",
            DisplayName = "Wan",
            ClipType = "wan",
            UsesModelSamplingShift = true,
            SupportsMoeExperts = true,
            ShowsLatentSourceMode = true,
        };

    /// <summary>Display order for the profile dropdown.</summary>
    public static IReadOnlyList<VideoWorkflowProfile> All { get; } = [Auto, Wan];

    public static VideoWorkflowProfile FromKey(string? key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Auto;

    /// <summary>
    /// Detects the workflow profile for a video UNet. Never returns <see cref="Auto"/>.
    /// Wan is the only family so far, so it is also the fallback for unrecognized names.
    /// </summary>
    public static VideoWorkflowProfile Detect(HybridModelFile? model)
    {
        _ = model;
        return Wan;
    }
}
