using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Detects the models Krea 2 needs, using only what is already installed locally:
/// a Krea 2 diffusion model (UNET), the full Qwen3-VL-4B text encoder and the Wan 2.1 VAE.
/// Nothing is offered for download — the files are large and the sources vary, so a missing
/// piece is reported by name and the user drops it into the matching shared folder.
/// </summary>
public class Krea2ModelManager : ILocalProviderModelManager
{
    public string ProviderId => BananaVisionProviderIds.Krea2;

    public string ProviderDisplayName => "Krea 2";

    public string DownloadDialogDescription =>
        "Krea 2 needs a Krea 2 diffusion model, the Qwen3-VL-4B text encoder and the Wan 2.1 VAE.";

    public bool AreModelsAvailable(IInferenceClientManager clientManager) =>
        GetMissingModelNames(clientManager).Any() is false;

    // No auto-download: see the class summary.
    public IEnumerable<RemoteResource> GetMissingModels(IInferenceClientManager clientManager) => [];

    public IEnumerable<string> GetMissingModelNames(IInferenceClientManager clientManager)
    {
        if (!clientManager.UnetModels.Any(m => m.Local != null && IsKreaUnet(m)))
            yield return "Krea 2 diffusion model";

        if (!clientManager.VaeModels.Any(m => m.Local != null && IsKreaVae(m.FileName)))
            yield return "Wan 2.1 VAE (wan_2.1_vae)";

        if (!clientManager.ClipModels.Any(m => m.Local != null && IsKreaTextEncoder(m.FileName)))
            yield return "Qwen3-VL 4B text encoder (qwen3vl_4b)";
    }

    /// <summary>
    /// Picks the models for a Krea 2 generation. The UNET is the user's dropdown choice when
    /// given, otherwise the first installed Krea 2 model; the VAE and encoder are whichever
    /// compatible files are installed.
    /// </summary>
    internal SelectedModels SelectModels(
        IInferenceClientManager clientManager,
        HybridModelFile? preferredUnet = null
    )
    {
        var unet =
            preferredUnet
            ?? clientManager.UnetModels.FirstOrDefault(m => m.Local != null && IsKreaUnet(m))
            ?? throw new InvalidOperationException("Krea 2 diffusion model not found");

        var vae =
            clientManager.VaeModels.FirstOrDefault(m => m.Local != null && IsKreaVae(m.FileName))
            ?? throw new InvalidOperationException(
                "Krea 2 needs the Wan 2.1 VAE (wan_2.1_vae.safetensors), which isn't installed."
            );

        var clip =
            clientManager.ClipModels.FirstOrDefault(m => m.Local != null && IsKreaTextEncoder(m.FileName))
            ?? throw new InvalidOperationException(
                "Krea 2 needs the full Qwen3-VL-4B text encoder (e.g. qwen3vl_4b_fp8_scaled.safetensors), which isn't installed."
            );

        return new SelectedModels(unet, vae, clip);
    }

    /// <summary>
    /// A Krea 2 diffusion model, by filename or CivitAI base-model metadata. Deliberately does
    /// not match Flux.1 Krea (<c>flux1-krea-dev</c>), which is a different architecture.
    /// </summary>
    internal static bool IsKreaUnet(HybridModelFile model)
    {
        if (ContainsKrea2(model.FileName))
            return true;

        var baseModel = model.Local?.ConnectedModelInfo?.BaseModel;
        return !string.IsNullOrEmpty(baseModel) && ContainsKrea2(baseModel);
    }

    private static bool ContainsKrea2(string text) =>
        text.Contains("krea2", StringComparison.OrdinalIgnoreCase)
        || text.Contains("krea-2", StringComparison.OrdinalIgnoreCase)
        || text.Contains("krea_2", StringComparison.OrdinalIgnoreCase)
        || text.Contains("krea 2", StringComparison.OrdinalIgnoreCase);

    // The full Qwen3-VL-4B (vision-language) encoder. Plain qwen_3_4b (Flux.2 Klein's encoder)
    // is a different file and cannot feed Krea 2.
    internal static bool IsKreaTextEncoder(string fileName) =>
        fileName.Contains("qwen3vl_4b", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("qwen3_vl_4b", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("qwen_3_vl_4b", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("qwen3-vl-4b", StringComparison.OrdinalIgnoreCase);

    internal static bool IsKreaVae(string fileName) =>
        fileName.Contains("wan_2.1_vae", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("wan2.1_vae", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("wan21_vae", StringComparison.OrdinalIgnoreCase);

    internal record SelectedModels(
        HybridModelFile UnetModel,
        HybridModelFile VaeModel,
        HybridModelFile ClipModel
    );
}
