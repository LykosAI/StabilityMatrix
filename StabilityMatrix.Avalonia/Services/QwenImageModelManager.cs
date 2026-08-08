using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manages Qwen Image text-to-image model requirements and validation.
/// The VAE and Qwen2.5-VL 7B text encoder are shared with Qwen Image Edit, so an existing
/// Edit install only needs the t2i UNET on top.
/// </summary>
public class QwenImageModelManager : ILocalProviderModelManager
{
    public string ProviderId => BananaVisionProviderIds.QwenImage;

    public string ProviderDisplayName => "Qwen Image";

    public string DownloadDialogDescription =>
        "Qwen Image requires the following models to run (the UNET alone is ~20 GB). "
        + "You can deselect any you already have installed elsewhere:";

    public bool AreModelsAvailable(IInferenceClientManager clientManager)
    {
        var hasUnet = clientManager.UnetModels.Any(m => m.Local != null && IsQwenImageUnet(m.FileName));

        var hasVae = clientManager.VaeModels.Any(m =>
            m.Local != null && m.FileName.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase)
        );

        var hasClip = clientManager.ClipModels.Any(m => m.Local != null && IsUsableQwenVlEncoder(m.FileName));

        return hasUnet && hasVae && hasClip;
    }

    public IEnumerable<RemoteResource> GetMissingModels(IInferenceClientManager clientManager)
    {
        var allModels = RemoteModels.QwenImageModels;
        var missing = new List<RemoteResource>();

        if (!clientManager.UnetModels.Any(m => m.Local != null && IsQwenImageUnet(m.FileName)))
        {
            var unetModel = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.DiffusionModels);
            if (unetModel.Url != null)
            {
                missing.Add(unetModel);
            }
        }

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

    public IEnumerable<string> GetMissingModelNames(IInferenceClientManager clientManager)
    {
        foreach (var model in GetMissingModels(clientManager))
        {
            var name = model.ContextType switch
            {
                SharedFolderType.DiffusionModels => "Qwen Image UNET",
                SharedFolderType.VAE => "Qwen Image VAE",
                SharedFolderType.TextEncoders => "Qwen 2.5 VL CLIP",
                _ => model.FileName,
            };
            yield return name;
        }
    }

    /// <summary>
    /// Select the best available models for Qwen Image (only selects LOCAL models).
    /// Prefers the 2512 build when several t2i UNETs are installed.
    /// </summary>
    internal SelectedModels SelectModels(IInferenceClientManager clientManager)
    {
        var unetModel =
            clientManager.UnetModels.FirstOrDefault(m =>
                m.Local != null
                && IsQwenImageUnet(m.FileName)
                && m.FileName.Contains("2512", StringComparison.OrdinalIgnoreCase)
            )
            ?? clientManager.UnetModels.FirstOrDefault(m => m.Local != null && IsQwenImageUnet(m.FileName))
            ?? throw new InvalidOperationException("Qwen Image UNET model not found");

        var vaeModel =
            clientManager.VaeModels.FirstOrDefault(m =>
                m.Local != null && m.FileName.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase)
            ) ?? throw new InvalidOperationException("Qwen Image VAE model not found");

        // Prefer an explicit 7B match, then any VL encoder without a size hint (could be a
        // renamed 7B). Wrong-size VL encoders load fine but die mid-sampling with a tensor
        // shape mismatch — same constraint as Qwen Image Edit.
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
                "Qwen Image requires the Qwen2.5-VL 7B text encoder. Download "
                    + "qwen_2.5_vl_7b_fp8_scaled.safetensors from "
                    + "huggingface.co/Comfy-Org/Qwen-Image_ComfyUI and place it in your TextEncoders folder."
            );

        return new SelectedModels(unetModel, vaeModel, clipModel);
    }

    // The t2i UNETs (qwen_image_2512_*, qwen_image_fp8_*) share their name stem with the
    // Edit UNETs (qwen_image_edit_*) and the VAE (qwen_image_vae), so both are excluded.
    internal static bool IsQwenImageUnet(string fileName) =>
        (
            fileName.Contains("qwen_image", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("qwen-image", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("qwenimage", StringComparison.OrdinalIgnoreCase)
        )
        && !fileName.Contains("edit", StringComparison.OrdinalIgnoreCase)
        && !fileName.Contains("vae", StringComparison.OrdinalIgnoreCase);

    private static bool IsQwenVlEncoder(string fileName) =>
        fileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
        && fileName.Contains("vl", StringComparison.OrdinalIgnoreCase);

    // Mirrors QwenImageEditModelManager: only the 2.5-VL 7B encoder (hidden size 3584)
    // works; "4b" excludes Krea 2's Qwen3-VL 4B encoder which also matches the loose check.
    private static readonly string[] WrongVlEncoderSizeHints = ["2b", "3b", "4b", "32b", "72b"];

    private static bool IsUsableQwenVlEncoder(string fileName) =>
        IsQwenVlEncoder(fileName)
        && !WrongVlEncoderSizeHints.Any(hint => fileName.Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Selected models for Qwen Image
    /// </summary>
    internal record SelectedModels(
        HybridModelFile UnetModel,
        HybridModelFile VaeModel,
        HybridModelFile ClipModel
    );
}
