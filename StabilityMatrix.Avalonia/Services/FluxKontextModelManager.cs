using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Manages Flux Kontext model requirements and validation
/// </summary>
public class FluxKontextModelManager : ILocalProviderModelManager
{
    public string ProviderId => BananaVisionProviderIds.FluxKontext;

    public string ProviderDisplayName => "Flux Kontext";

    public string DownloadDialogDescription =>
        "Flux Kontext requires the following models to run. You can deselect any you already have installed elsewhere:";

    /// <summary>
    /// Check if all required models are available (only checks LOCAL models, not remote definitions)
    /// </summary>
    public bool AreModelsAvailable(IInferenceClientManager clientManager)
    {
        var hasUnet = clientManager.UnetModels.Any(m =>
            m.Local != null
            && // Only check LOCAL models
            (
                m.FileName.Contains("flux1-dev-kontext", StringComparison.OrdinalIgnoreCase)
                || m.FileName.Contains("flux-kontext", StringComparison.OrdinalIgnoreCase)
            )
        );

        var hasVae = clientManager.VaeModels.Any(m =>
            m.Local != null
            && // Only check LOCAL models
            m.FileName.Equals("ae.safetensors", StringComparison.OrdinalIgnoreCase)
        );

        var hasClip1 = clientManager.ClipModels.Any(m =>
            m.Local != null
            && // Only check LOCAL models
            m.FileName.Contains("clip_l", StringComparison.OrdinalIgnoreCase)
        );

        var hasClip2 = clientManager.ClipModels.Any(m =>
            m.Local != null
            && // Only check LOCAL models
            (
                m.FileName.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
                || m.FileName.Contains("t5-xxl", StringComparison.OrdinalIgnoreCase)
            )
        );

        return hasUnet && hasVae && hasClip1 && hasClip2;
    }

    /// <summary>
    /// Get list of missing models as RemoteResource for download
    /// </summary>
    public IEnumerable<RemoteResource> GetMissingModels(IInferenceClientManager clientManager)
    {
        var allModels = RemoteModels.FluxKontextModels;
        var missing = new List<RemoteResource>();

        // Check for UNET model (only LOCAL models)
        if (
            !clientManager.UnetModels.Any(m =>
                m.Local != null
                && (
                    m.FileName.Contains("flux1-dev-kontext", StringComparison.OrdinalIgnoreCase)
                    || m.FileName.Contains("flux-kontext", StringComparison.OrdinalIgnoreCase)
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
                m.Local != null && m.FileName.Equals("ae.safetensors", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var vaeModel = allModels.FirstOrDefault(m => m.ContextType is SharedFolderType.VAE);
            if (vaeModel.Url != null)
            {
                missing.Add(vaeModel);
            }
        }

        // Check for CLIP-L model (only LOCAL models)
        if (
            !clientManager.ClipModels.Any(m =>
                m.Local != null && m.FileName.Contains("clip_l", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            var clipModel = allModels.FirstOrDefault(m =>
                m.ContextType is SharedFolderType.TextEncoders
                && m.FileName.Contains("clip_l", StringComparison.OrdinalIgnoreCase)
            );
            if (clipModel.Url != null)
            {
                missing.Add(clipModel);
            }
        }

        // Check for T5-XXL model (only LOCAL models)
        if (
            !clientManager.ClipModels.Any(m =>
                m.Local != null
                && (
                    m.FileName.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
                    || m.FileName.Contains("t5-xxl", StringComparison.OrdinalIgnoreCase)
                )
            )
        )
        {
            var t5Model = allModels.FirstOrDefault(m =>
                m.ContextType is SharedFolderType.TextEncoders
                && m.FileName.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
            );
            if (t5Model.Url != null)
            {
                missing.Add(t5Model);
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
                SharedFolderType.DiffusionModels => "Flux Kontext UNET",
                SharedFolderType.VAE => "Flux VAE",
                SharedFolderType.TextEncoders
                    when model.FileName.Contains("clip_l", StringComparison.OrdinalIgnoreCase) => "CLIP-L",
                SharedFolderType.TextEncoders
                    when model.FileName.Contains("t5xxl", StringComparison.OrdinalIgnoreCase) => "T5-XXL",
                _ => model.FileName,
            };
            yield return name;
        }
    }

    /// <summary>
    /// Select the best available models for Flux Kontext (only selects LOCAL models)
    /// </summary>
    internal FluxKontextWorkflowBuilder.SelectedModels SelectModels(IInferenceClientManager clientManager)
    {
        var unetModel =
            clientManager.UnetModels.FirstOrDefault(m =>
                m.Local != null
                && (
                    m.FileName.Contains("flux1-dev-kontext", StringComparison.OrdinalIgnoreCase)
                    || m.FileName.Contains("flux-kontext", StringComparison.OrdinalIgnoreCase)
                )
            ) ?? throw new InvalidOperationException("Flux Kontext UNET model not found");

        var vaeModel =
            clientManager.VaeModels.FirstOrDefault(m =>
                m.Local != null && m.FileName.Equals("ae.safetensors", StringComparison.OrdinalIgnoreCase)
            ) ?? throw new InvalidOperationException("Flux VAE model not found");

        var clip1Model =
            clientManager.ClipModels.FirstOrDefault(m =>
                m.Local != null && m.FileName.Contains("clip_l", StringComparison.OrdinalIgnoreCase)
            ) ?? throw new InvalidOperationException("CLIP-L model not found");

        var clip2Model =
            clientManager.ClipModels.FirstOrDefault(m =>
                m.Local != null
                && (
                    m.FileName.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
                    || m.FileName.Contains("t5-xxl", StringComparison.OrdinalIgnoreCase)
                )
            ) ?? throw new InvalidOperationException("T5-XXL model not found");

        return new FluxKontextWorkflowBuilder.SelectedModels(unetModel, vaeModel, clip1Model, clip2Model);
    }
}
