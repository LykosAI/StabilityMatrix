using System;
using System.Collections.Generic;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.HuggingFace;

/// <summary>
/// Helpers for the live HuggingFace browser: guessing a destination
/// <see cref="SharedFolderType"/> from a file path, and parsing repo ids from URLs.
/// </summary>
public static class HuggingFaceFolderInference
{
    /// <summary>
    /// The destination folders offered in the per-file destination dropdown,
    /// ordered roughly by how common they are for downloaded models.
    /// </summary>
    public static IReadOnlyList<SharedFolderType> SelectableFolders { get; } =
        new[]
        {
            SharedFolderType.StableDiffusion,
            SharedFolderType.DiffusionModels,
            SharedFolderType.Lora,
            SharedFolderType.VAE,
            SharedFolderType.TextEncoders,
            SharedFolderType.ClipVision,
            SharedFolderType.ControlNet,
            SharedFolderType.IpAdapter,
            SharedFolderType.T2IAdapter,
            SharedFolderType.StyleModels,
            SharedFolderType.Embeddings,
            SharedFolderType.Ultralytics,
            SharedFolderType.Sams,
            SharedFolderType.AudioEncoders,
            SharedFolderType.ESRGAN,
        };

    /// <summary>
    /// Guess the most appropriate destination folder for a file based on its path/name.
    /// Falls back to <see cref="SharedFolderType.StableDiffusion"/> for generic checkpoints.
    /// </summary>
    public static SharedFolderType Infer(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        var name = p.Contains('/') ? p[(p.LastIndexOf('/') + 1)..] : p;

        // Order matters: more specific matches first.
        if (Contains(p, "clip_vision", "clip-vision", "clipvision"))
            return SharedFolderType.ClipVision;

        if (Contains(p, "controlnet", "control_net", "control-net", "control_v", "control-v"))
            return SharedFolderType.ControlNet;

        if (Contains(p, "t2i", "t2i_adapter", "t2i-adapter"))
            return SharedFolderType.T2IAdapter;

        if (Contains(p, "ip-adapter", "ip_adapter", "ipadapter"))
            return SharedFolderType.IpAdapter;

        if (
            Contains(p, "text_encoder", "text_encoders", "/clip/")
            || StartsWith(name, "clip_", "clip-", "t5", "umt5", "llava", "qwen_3")
        )
            return SharedFolderType.TextEncoders;

        if (Contains(p, "vae") || StartsWith(name, "ae."))
            return SharedFolderType.VAE;

        if (Contains(p, "lora", "loras"))
            return SharedFolderType.Lora;

        if (Contains(p, "embedding", "textual_inversion"))
            return SharedFolderType.Embeddings;

        if (Contains(p, "style_model", "style_models", "redux"))
            return SharedFolderType.StyleModels;

        if (Contains(p, "audio_encoder", "audio-encoder"))
            return SharedFolderType.AudioEncoders;

        if (Contains(p, "upscal", "esrgan"))
            return SharedFolderType.ESRGAN;

        if (
            name.EndsWith(".gguf")
            || Contains(p, "unet", "diffusion_model", "diffusion_models", "transformer")
        )
            return SharedFolderType.DiffusionModels;

        return SharedFolderType.StableDiffusion;
    }

    /// <summary>
    /// Attempt to parse a HuggingFace repo id (<c>owner/name</c>) from raw user input,
    /// accepting bare ids, <c>huggingface.co</c> URLs, and <c>/tree/</c> or <c>/blob/</c> links.
    /// </summary>
    public static bool TryParseRepoId(string? input, out string repoId)
    {
        repoId = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var value = input.Trim();

        // Strip a URL scheme + host if present.
        if (value.Contains("huggingface.co", StringComparison.OrdinalIgnoreCase))
        {
            var idx = value.IndexOf("huggingface.co", StringComparison.OrdinalIgnoreCase);
            value = value[(idx + "huggingface.co".Length)..].TrimStart('/');
        }
        else if (value.Contains("://"))
        {
            // Some other URL - not a HF repo.
            return false;
        }

        // Drop any query/fragment.
        foreach (var sep in new[] { '?', '#' })
        {
            var qi = value.IndexOf(sep);
            if (qi >= 0)
                value = value[..qi];
        }

        var segments = value.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        // Take the first two segments as owner/name; ignore /tree/main, /blob/..., etc.
        var owner = segments[0];
        var name = segments[1];

        // Reject obvious non-repo first segments (route prefixes).
        if (owner is "models" or "datasets" or "spaces" or "api")
            return false;

        repoId = $"{owner}/{name}";
        return true;
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool StartsWith(string value, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
