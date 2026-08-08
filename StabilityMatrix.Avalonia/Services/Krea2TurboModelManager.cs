using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manages Krea 2 Turbo model requirements and validation. Turbo-only by design: the Raw
/// variant needs full 52-step sampling with settings Krea hasn't published for ComfyUI, so
/// Raw users are served by the Inference tab's Krea 2 Raw profile instead of the Image Lab.
/// </summary>
public class Krea2TurboModelManager : ILocalProviderModelManager
{
    public string ProviderId => BananaVisionProviderIds.Krea2Turbo;

    public string ProviderDisplayName => "Krea 2 Turbo";

    public string DownloadDialogDescription =>
        "Krea 2 Turbo requires the following models to run. You can deselect any you already have installed elsewhere:";

    public bool AreModelsAvailable(IInferenceClientManager clientManager)
    {
        var hasUnet = clientManager.UnetModels.Any(m => m.Local != null && IsKrea2TurboUnet(m.FileName));

        var hasVae = clientManager.VaeModels.Any(m =>
            m.Local != null && m.FileName.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase)
        );

        var hasClip = clientManager.ClipModels.Any(m => m.Local != null && IsKrea2TextEncoder(m.FileName));

        return hasUnet && hasVae && hasClip;
    }

    public IEnumerable<RemoteResource> GetMissingModels(IInferenceClientManager clientManager)
    {
        var allModels = RemoteModels.Krea2TurboModels;
        var missing = new List<RemoteResource>();

        if (!clientManager.UnetModels.Any(m => m.Local != null && IsKrea2TurboUnet(m.FileName)))
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

        if (!clientManager.ClipModels.Any(m => m.Local != null && IsKrea2TextEncoder(m.FileName)))
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
                SharedFolderType.DiffusionModels => "Krea 2 Turbo UNET",
                SharedFolderType.VAE => "Qwen Image VAE",
                SharedFolderType.TextEncoders => "Qwen3-VL 4B text encoder",
                _ => model.FileName,
            };
            yield return name;
        }
    }

    /// <summary>
    /// Select the best available models for Krea 2 Turbo (only selects LOCAL models).
    /// </summary>
    internal SelectedModels SelectModels(IInferenceClientManager clientManager)
    {
        var unetModel =
            clientManager.UnetModels.FirstOrDefault(m => m.Local != null && IsKrea2TurboUnet(m.FileName))
            ?? throw new InvalidOperationException("Krea 2 Turbo UNET model not found");

        var vaeModel =
            clientManager.VaeModels.FirstOrDefault(m =>
                m.Local != null && m.FileName.Contains("qwen_image_vae", StringComparison.OrdinalIgnoreCase)
            ) ?? throw new InvalidOperationException("Qwen Image VAE model not found");

        var clipModel =
            clientManager.ClipModels.FirstOrDefault(m => m.Local != null && IsKrea2TextEncoder(m.FileName))
            ?? throw new InvalidOperationException(
                "Krea 2 requires the Qwen3-VL 4B text encoder. Download "
                    + "qwen3vl_4b_fp8_scaled.safetensors from huggingface.co/Comfy-Org/Krea-2 "
                    + "and place it in your TextEncoders folder."
            );

        return new SelectedModels(unetModel, vaeModel, clipModel);
    }

    // Turbo variants only (krea2_turbo_fp8_scaled, community turbo quants). Raw UNETs are
    // deliberately not matched — see the class doc.
    internal static bool IsKrea2TurboUnet(string fileName) =>
        (
            fileName.Contains("krea2", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("krea-2", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("krea_2", StringComparison.OrdinalIgnoreCase)
        ) && fileName.Contains("turbo", StringComparison.OrdinalIgnoreCase);

    // Krea 2's encoder is Qwen3-VL 4B (qwen3vl_4b_fp8_scaled) — distinct from Klein's
    // text-only qwen_3_4b (no "vl") and Qwen Image's qwen_2.5_vl_7b (no "qwen3").
    internal static bool IsKrea2TextEncoder(string fileName) =>
        fileName.Contains("qwen3vl", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("qwen3-vl", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("qwen3_vl", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Selected models for Krea 2 Turbo
    /// </summary>
    internal record SelectedModels(
        HybridModelFile UnetModel,
        HybridModelFile VaeModel,
        HybridModelFile ClipModel
    );
}
