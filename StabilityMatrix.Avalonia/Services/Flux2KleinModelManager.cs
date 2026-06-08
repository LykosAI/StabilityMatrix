using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manages Flux.2 Klein model requirements and validation.
/// Detection is loose enough to pick up both Klein 4B (qwen_3_4b text encoder) and
/// Klein 9B (qwen_3_8b text encoder) variants if the user has downloaded them.
/// </summary>
public class Flux2KleinModelManager : ILocalProviderModelManager
{
    public string ProviderId => BananaVisionProviderIds.Flux2Klein;

    public string ProviderDisplayName => "Flux.2 Klein";

    public string DownloadDialogDescription =>
        "Flux.2 Klein requires the following models to run. You can deselect any you already have installed elsewhere:";

    public bool AreModelsAvailable(IInferenceClientManager clientManager)
    {
        var hasUnet = clientManager.UnetModels.Any(m => m.Local != null && IsKleinUnet(m.FileName));
        var hasVae = clientManager.VaeModels.Any(m => m.Local != null && IsFlux2Vae(m.FileName));
        var hasClip = clientManager.ClipModels.Any(m => m.Local != null && IsKleinTextEncoder(m.FileName));

        return hasUnet && hasVae && hasClip;
    }

    public IEnumerable<RemoteResource> GetMissingModels(IInferenceClientManager clientManager)
    {
        var allModels = RemoteModels.Flux2KleinModels;
        var missing = new List<RemoteResource>();

        if (!clientManager.UnetModels.Any(m => m.Local != null && IsKleinUnet(m.FileName)))
        {
            var unet = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.DiffusionModels);
            if (unet.Url != null)
            {
                missing.Add(unet);
            }
        }

        if (!clientManager.VaeModels.Any(m => m.Local != null && IsFlux2Vae(m.FileName)))
        {
            var vae = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.VAE);
            if (vae.Url != null)
            {
                missing.Add(vae);
            }
        }

        if (!clientManager.ClipModels.Any(m => m.Local != null && IsKleinTextEncoder(m.FileName)))
        {
            var clip = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.TextEncoders);
            if (clip.Url != null)
            {
                missing.Add(clip);
            }
        }

        return missing;
    }

    public IEnumerable<string> GetMissingModelNames(IInferenceClientManager clientManager)
    {
        foreach (var model in GetMissingModels(clientManager))
        {
            var name = model.ContextType switch
            {
                SharedFolderType.DiffusionModels => "Flux.2 Klein 4B UNET",
                SharedFolderType.VAE => "Flux.2 VAE",
                SharedFolderType.TextEncoders => "Qwen3 4B text encoder",
                _ => model.FileName,
            };
            yield return name;
        }
    }

    /// <summary>
    /// Select the best available models for Flux.2 Klein (only LOCAL models).
    /// When the selected UNET is the 9B variant, prefers the matching qwen_3_8b text encoder;
    /// when 4B, prefers qwen_3_4b. Falls back to whichever encoder is present.
    /// </summary>
    internal SelectedModels SelectModels(
        IInferenceClientManager clientManager,
        HybridModelFile? preferredUnet = null
    )
    {
        var unetModel =
            preferredUnet
            ?? clientManager.UnetModels.FirstOrDefault(m => m.Local != null && IsKleinUnet(m.FileName))
            ?? throw new InvalidOperationException("Flux.2 Klein UNET model not found");

        var vaeModel =
            clientManager.VaeModels.FirstOrDefault(m => m.Local != null && IsFlux2Vae(m.FileName))
            ?? throw new InvalidOperationException("Flux.2 VAE model not found");

        // Match the text encoder to the selected UNET variant. The 4B UNET expects
        // qwen_3_4b (~4B params, hidden_dim 2560) and the 9B UNET expects qwen_3_8b
        // (~8B params, hidden_dim 4096) — pairing the wrong size produces a tensor
        // shape mismatch deep inside the sampler, so we fail fast here with a clear
        // message rather than silently substituting the other size.
        var preferredEncoderSize = GetExpectedEncoderSize(unetModel);

        var clipModel =
            clientManager.ClipModels.FirstOrDefault(m =>
                m.Local != null
                && IsKleinTextEncoder(m.FileName)
                && MatchesEncoderSize(m.FileName, preferredEncoderSize)
            )
            ?? throw new InvalidOperationException(
                preferredEncoderSize == "8b"
                    ? "Klein 9B requires the qwen_3_8b text encoder, which isn't installed. Download qwen_3_8b_fp8mixed.safetensors (or _fp4mixed / _bf16) from huggingface.co/Comfy-Org/flux2-klein-9B and place it in your TextEncoders folder."
                    : "Klein 4B requires the qwen_3_4b text encoder, which isn't installed. Download qwen_3_4b.safetensors (or _fp4_flux2) from huggingface.co/Comfy-Org/flux2-klein-4B and place it in your TextEncoders folder."
            );

        return new SelectedModels(unetModel, vaeModel, clipModel);
    }

    /// <summary>
    /// Detects which Qwen3 text-encoder size a Klein UNET (or Klein-derived merge/fine-tune)
    /// pairs with. Checks the connected CivitAI metadata first — `BaseModel`, `ModelName`,
    /// `VersionName`, `VersionDescription`, and `TrainedWords` — because filenames on
    /// community merges often don't include a "9b" / "4b" hint. Falls back to the filename,
    /// then defaults to "4b" (matches the auto-downloaded Apache 2.0 Klein 4B variant).
    /// </summary>
    private static string GetExpectedEncoderSize(HybridModelFile unetModel)
    {
        var info = unetModel.Local?.ConnectedModelInfo;

        // Collect every text field worth scanning, skipping nulls.
        var haystacks = new List<string>(8) { unetModel.FileName };
        if (info != null)
        {
            if (!string.IsNullOrEmpty(info.BaseModel))
                haystacks.Add(info.BaseModel);
            if (!string.IsNullOrEmpty(info.ModelName))
                haystacks.Add(info.ModelName);
            if (!string.IsNullOrEmpty(info.VersionName))
                haystacks.Add(info.VersionName);
            if (!string.IsNullOrEmpty(info.VersionDescription))
                haystacks.Add(info.VersionDescription);
            if (info.TrainedWords != null)
                haystacks.AddRange(info.TrainedWords);
        }

        // 9B / 8B-encoder signals — checked first so e.g. "Flux.2 Klein 9B" in BaseModel
        // beats a generic filename like "myMerge.safetensors". The "9b" / "9-b" / "9 b"
        // patterns cover both spaced and dashed variants.
        if (haystacks.Any(IsNineBSignal))
            return "8b";

        // 4B-encoder signals — explicit "4b" / "Klein 4B" beats the default fallback.
        if (haystacks.Any(IsFourBSignal))
            return "4b";

        // No hint either way — assume 4B (the auto-downloaded Apache 2.0 default).
        return "4b";
    }

    private static bool IsNineBSignal(string text) =>
        text.Contains("9b", StringComparison.OrdinalIgnoreCase)
        || text.Contains("9 b", StringComparison.OrdinalIgnoreCase)
        || text.Contains("9-b", StringComparison.OrdinalIgnoreCase)
        || text.Contains("klein 9", StringComparison.OrdinalIgnoreCase)
        || text.Contains("klein-9", StringComparison.OrdinalIgnoreCase)
        || text.Contains("klein_9", StringComparison.OrdinalIgnoreCase);

    private static bool IsFourBSignal(string text) =>
        text.Contains("4b", StringComparison.OrdinalIgnoreCase)
        || text.Contains("4 b", StringComparison.OrdinalIgnoreCase)
        || text.Contains("4-b", StringComparison.OrdinalIgnoreCase)
        || text.Contains("klein 4", StringComparison.OrdinalIgnoreCase)
        || text.Contains("klein-4", StringComparison.OrdinalIgnoreCase)
        || text.Contains("klein_4", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesEncoderSize(string encoderFileName, string size) =>
        size switch
        {
            "8b" => encoderFileName.Contains("qwen_3_8b", StringComparison.OrdinalIgnoreCase)
                || encoderFileName.Contains("qwen3_8b", StringComparison.OrdinalIgnoreCase),
            "4b" => encoderFileName.Contains("qwen_3_4b", StringComparison.OrdinalIgnoreCase)
                || encoderFileName.Contains("qwen3_4b", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

    private static bool IsKleinUnet(string fileName) =>
        fileName.Contains("flux-2-klein", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("flux2-klein", StringComparison.OrdinalIgnoreCase);

    private static bool IsFlux2Vae(string fileName) =>
        // Distilled variants use flux2-vae.safetensors; base variants use
        // full_encoder_small_decoder.safetensors. Both are valid Flux.2 VAEs.
        fileName.Contains("flux2-vae", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("flux2_vae", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("full_encoder_small_decoder", StringComparison.OrdinalIgnoreCase);

    // Match the Qwen3 text encoders that Klein uses (qwen_3_4b for 4B model, qwen_3_8b for 9B).
    // Note: deliberately excludes Qwen 2.5 VL encoders used by Qwen Image Edit
    // (those have "vl" in the filename and use the older "_2.5_" version tag).
    private static bool IsKleinTextEncoder(string fileName) =>
        (
            fileName.Contains("qwen_3_4b", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("qwen_3_8b", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("qwen3_4b", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("qwen3_8b", StringComparison.OrdinalIgnoreCase)
        ) && !fileName.Contains("vl", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Selected models for Flux.2 Klein
    /// </summary>
    internal record SelectedModels(
        HybridModelFile UnetModel,
        HybridModelFile VaeModel,
        HybridModelFile ClipModel
    );
}
