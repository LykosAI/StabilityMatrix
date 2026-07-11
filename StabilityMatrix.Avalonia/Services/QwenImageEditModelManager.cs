using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manages Qwen Image Edit model requirements and validation
/// </summary>
public class QwenImageEditModelManager : ILocalProviderModelManager
{
    public string ProviderId => BananaVisionProviderIds.QwenImageEdit;

    public string ProviderDisplayName => "Qwen Image Edit";

    public string DownloadDialogDescription =>
        "Qwen Image Edit requires the following models to run. You can deselect any you already have installed elsewhere:";

    /// <summary>
    /// Check if all required models are available (only checks LOCAL models, not remote definitions)
    /// </summary>
    public bool AreModelsAvailable(IInferenceClientManager clientManager)
    {
        var hasUnet = clientManager.UnetModels.Any(m =>
            m.Local != null
            && // Only check LOCAL models
            (
                m.FileName.Contains("qwen_image_edit", StringComparison.OrdinalIgnoreCase)
                || m.FileName.Contains("qwen-image-edit", StringComparison.OrdinalIgnoreCase)
            )
        );

        var hasVae = clientManager.VaeModels.Any(m =>
            m.Local != null
            && // Only check LOCAL models
            m.FileName.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase)
        );

        var hasClip = clientManager.ClipModels.Any(m =>
            m.Local != null
            && // Only check LOCAL models
            IsUsableQwenVlEncoder(m.FileName)
        );

        return hasUnet && hasVae && hasClip;
    }

    /// <summary>
    /// Get list of missing models as RemoteResource for download
    /// </summary>
    public IEnumerable<RemoteResource> GetMissingModels(IInferenceClientManager clientManager)
    {
        var allModels = RemoteModels.QwenImageEditModels;
        var missing = new List<RemoteResource>();

        // Check for UNET model (only LOCAL models)
        if (
            !clientManager.UnetModels.Any(m =>
                m.Local != null
                && (
                    m.FileName.Contains("qwen_image_edit", StringComparison.OrdinalIgnoreCase)
                    || m.FileName.Contains("qwen-image-edit", StringComparison.OrdinalIgnoreCase)
                )
            )
        )
        {
            var unetModel = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.DiffusionModels);
            if (unetModel.Url != null)
            {
                missing.Add(unetModel);
            }
        }

        // Check for VAE model (only LOCAL models)
        if (
            !clientManager.VaeModels.Any(m =>
                m.Local != null && m.FileName.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var vaeModel = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.VAE);
            if (vaeModel.Url != null)
            {
                missing.Add(vaeModel);
            }
        }

        // Check for Qwen CLIP model (only LOCAL models). Wrong-size VL encoders (2B/3B)
        // don't count — they fail at sampling — so the 7B download is still offered.
        if (!clientManager.ClipModels.Any(m => m.Local != null && IsUsableQwenVlEncoder(m.FileName)))
        {
            var clipModel = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.TextEncoders);
            if (clipModel.Url != null)
            {
                missing.Add(clipModel);
            }
        }

        return missing;
    }

    /// <summary>
    /// Get human-readable names for missing models (for status display)
    /// </summary>
    public IEnumerable<string> GetMissingModelNames(IInferenceClientManager clientManager)
    {
        var missing = GetMissingModels(clientManager);

        foreach (var model in missing)
        {
            var name = model.ContextType switch
            {
                SharedFolderType.DiffusionModels => "Qwen Image Edit UNET",
                SharedFolderType.VAE => "Qwen Image VAE",
                SharedFolderType.TextEncoders => "Qwen 2.5 VL CLIP",
                _ => model.FileName,
            };
            yield return name;
        }
    }

    /// <summary>
    /// Select the best available models for Qwen Image Edit (only selects LOCAL models)
    /// </summary>
    internal SelectedModels SelectModels(IInferenceClientManager clientManager)
    {
        var unetModel =
            clientManager.UnetModels.FirstOrDefault(m =>
                m.Local != null
                && (
                    m.FileName.Contains("qwen_image_edit", StringComparison.OrdinalIgnoreCase)
                    || m.FileName.Contains("qwen-image-edit", StringComparison.OrdinalIgnoreCase)
                )
            ) ?? throw new InvalidOperationException("Qwen Image Edit UNET model not found");

        var vaeModel =
            clientManager.VaeModels.FirstOrDefault(m =>
                m.Local != null && m.FileName.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase)
            ) ?? throw new InvalidOperationException("Qwen Image VAE model not found");

        // Prefer an explicit 7B match, then any VL encoder without a size hint (could be a
        // renamed 7B). Smaller VL encoders (e.g. the 3B, hidden size 2048) load fine but die
        // mid-sampling with "expected input with shape [*, 3584]", so fail fast with a clear
        // message instead of silently picking one.
        var clipModel =
            clientManager.ClipModels.FirstOrDefault(m =>
                m.Local != null
                && IsQwenVlEncoder(m.FileName)
                && m.FileName.Contains("7b", StringComparison.OrdinalIgnoreCase)
            )
            ?? clientManager.ClipModels.FirstOrDefault(m =>
                m.Local != null && IsUsableQwenVlEncoder(m.FileName)
            )
            ?? throw new InvalidOperationException(
                clientManager.ClipModels.Any(m => m.Local != null && IsQwenVlEncoder(m.FileName))
                    ? "Qwen Image Edit requires the Qwen2.5-VL 7B text encoder, but only a different-size "
                        + "VL encoder was found (smaller variants fail with a tensor shape mismatch). "
                        + "Download qwen_2.5_vl_7b_fp8_scaled.safetensors from "
                        + "huggingface.co/Comfy-Org/Qwen-Image_ComfyUI and place it in your TextEncoders folder."
                    : "Qwen 2.5 VL CLIP model not found"
            );

        return new SelectedModels(unetModel, vaeModel, clipModel);
    }

    private static bool IsQwenVlEncoder(string fileName) =>
        fileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
        && fileName.Contains("vl", StringComparison.OrdinalIgnoreCase);

    // Qwen Image Edit pairs with the Qwen2.5-VL **7B** encoder (hidden size 3584). Other
    // sizes produce "Given normalized_shape=[3584], expected input with shape [*, 3584]"
    // deep in the sampler, so they are treated as not installed. Files without any size
    // hint are accepted (likely a renamed 7B).
    private static readonly string[] WrongVlEncoderSizeHints = ["2b", "3b", "32b", "72b"];

    private static bool IsUsableQwenVlEncoder(string fileName) =>
        IsQwenVlEncoder(fileName)
        && !WrongVlEncoderSizeHints.Any(hint => fileName.Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Selected models for Qwen Image Edit
    /// </summary>
    internal record SelectedModels(
        HybridModelFile UnetModel,
        HybridModelFile VaeModel,
        HybridModelFile ClipModel
    );
}
