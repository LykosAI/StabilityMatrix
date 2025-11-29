using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Services.ImageGeneration;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Interface for local provider model managers.
/// Each local provider that requires specific models should implement this.
/// </summary>
public interface ILocalProviderModelManager
{
    /// <summary>
    /// The provider ID this manager handles
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Display name for the provider (used in dialog titles)
    /// </summary>
    string ProviderDisplayName { get; }

    /// <summary>
    /// Description shown in the download dialog
    /// </summary>
    string DownloadDialogDescription { get; }

    /// <summary>
    /// Check if all required models are available locally
    /// </summary>
    bool AreModelsAvailable(IInferenceClientManager clientManager);

    /// <summary>
    /// Get list of missing models as RemoteResource for download
    /// </summary>
    IEnumerable<RemoteResource> GetMissingModels(IInferenceClientManager clientManager);

    /// <summary>
    /// Get human-readable names for missing models (for status display)
    /// </summary>
    IEnumerable<string> GetMissingModelNames(IInferenceClientManager clientManager);
}
