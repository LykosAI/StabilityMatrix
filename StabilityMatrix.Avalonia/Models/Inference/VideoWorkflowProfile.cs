using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// A model-family workflow profile for the generic video tabs: the single owner of every
/// per-family fact video Inference needs (clip type, which family-specific controls to show,
/// frame grid). Adding a video model family means declaring one new instance here; graph
/// assembly dispatches on the resolved profile at one point in each tab. The video
/// counterpart of <see cref="InferenceWorkflowProfile"/>.
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

    /// <summary>Image-to-video conditions on a CLIP Vision encode of the input image (Wan 2.1).</summary>
    public bool UsesClipVision { get; init; }

    /// <summary>
    /// The family samples a packed audio+video latent and needs a second, audio VAE to decode
    /// the soundtrack (MiniMax H3).
    /// </summary>
    public bool UsesAudioVae { get; init; }

    /// <summary>
    /// The family has negative conditioning / CFG. When false the negative prompt is hidden
    /// and sampling runs uncond-free (CFG 1).
    /// </summary>
    public bool SupportsNegativePrompt { get; init; } = true;

    /// <summary>
    /// Frame-count grid the family requires, as stride/offset: valid lengths are
    /// k * stride + offset. Null = any length.
    /// </summary>
    public (int Stride, int Offset)? FrameGrid { get; init; }

    /// <summary>Rounds a frame count up onto <see cref="FrameGrid"/> (identity when null).</summary>
    public int SnapFrames(int frames)
    {
        if (FrameGrid is not { } grid)
            return frames;

        var (stride, offset) = grid;
        var min = Math.Max(frames, offset);
        return min + (offset - min % stride + stride * 2) % stride;
    }

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
            UsesClipVision = true,
        };

    /// <summary>
    /// MiniMax H3 — omni-modal video with native stereo audio (needs ComfyUI >= 0.33). One
    /// fl2va model covers t2v and first-frame i2v; the prompt is encoded by the conditioning
    /// node itself, uncond-free.
    /// </summary>
    public static VideoWorkflowProfile MiniMaxH3 { get; } =
        new()
        {
            Key = "MiniMaxH3",
            DisplayName = "MiniMax H3",
            ClipType = "minimax",
            UsesAudioVae = true,
            SupportsNegativePrompt = false,
            // 24 fps, 17 frames per block: valid lengths are 17k + 5.
            FrameGrid = (17, 5),
        };

    /// <summary>Display order for the profile dropdown.</summary>
    public static IReadOnlyList<VideoWorkflowProfile> All { get; } = [Auto, Wan, MiniMaxH3];

    public static VideoWorkflowProfile FromKey(string? key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Auto;

    /// <summary>
    /// Detects the workflow profile for a video UNet from its filename. Never returns
    /// <see cref="Auto"/>; Wan was the only family before profiles existed, so it stays the
    /// fallback for unrecognized names.
    /// </summary>
    public static VideoWorkflowProfile Detect(HybridModelFile? model)
    {
        var name = model?.RelativePath ?? string.Empty;

        if (name.Contains("minimax", StringComparison.OrdinalIgnoreCase))
            return MiniMaxH3;

        return Wan;
    }
}
