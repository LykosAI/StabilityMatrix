using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manages Qwen Image Edit model requirements and validation
/// </summary>
public class QwenImageEditModelManager : ILocalProviderModelManager
{
    public string ProviderId => "qwen-image-edit";

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
            m.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
            && m.FileName.Contains("vl", StringComparison.OrdinalIgnoreCase)
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

        // Check for Qwen CLIP model (only LOCAL models)
        if (
            !clientManager.ClipModels.Any(m =>
                m.Local != null
                && m.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
                && m.FileName.Contains("vl", StringComparison.OrdinalIgnoreCase)
            )
        )
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

        var clipModel =
            clientManager.ClipModels.FirstOrDefault(m =>
                m.Local != null
                && m.FileName.Contains("qwen", StringComparison.OrdinalIgnoreCase)
                && m.FileName.Contains("vl", StringComparison.OrdinalIgnoreCase)
            ) ?? throw new InvalidOperationException("Qwen 2.5 VL CLIP model not found");

        return new SelectedModels(unetModel, vaeModel, clipModel);
    }

    /// <summary>
    /// Selected models for Qwen Image Edit
    /// </summary>
    internal record SelectedModels(
        HybridModelFile UnetModel,
        HybridModelFile VaeModel,
        HybridModelFile ClipModel
    );
}
