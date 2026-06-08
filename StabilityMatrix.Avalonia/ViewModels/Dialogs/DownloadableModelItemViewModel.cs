using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// ViewModel for a single downloadable model item in the missing models dialog.
/// Wraps a RemoteResource with selection and progress state.
/// </summary>
public partial class DownloadableModelItemViewModel(RemoteResource resource, string? displayName = null)
    : ViewModelBase
{
    /// <summary>
    /// The underlying remote resource
    /// </summary>
    public RemoteResource Resource { get; } = resource;

    /// <summary>
    /// Whether this item is selected for download
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    /// <summary>
    /// Whether this item is currently downloading
    /// </summary>
    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    /// <summary>
    /// Whether this item has completed downloading
    /// </summary>
    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    /// <summary>
    /// Whether this item failed to download
    /// </summary>
    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    /// <summary>
    /// Current download progress (0-100)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial double Progress { get; set; }

    /// <summary>
    /// File size in bytes (fetched asynchronously)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileSizeText))]
    public partial long FileSize { get; set; }

    /// <summary>
    /// Status message for the download
    /// </summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>
    /// Display name for the model
    /// </summary>
    public string DisplayName { get; } = displayName ?? GetDefaultDisplayName(resource);

    /// <summary>
    /// Type badge text (e.g., "UNET", "VAE", "CLIP")
    /// </summary>
    public string TypeBadge { get; } = GetTypeBadge(resource);

    /// <summary>
    /// File name
    /// </summary>
    public string FileName => Resource.FileName;

    /// <summary>
    /// Formatted file size text
    /// </summary>
    public string? FileSizeText => FileSize > 0 ? Size.FormatBase10Bytes(FileSize) : null;

    /// <summary>
    /// Progress text for display
    /// </summary>
    public string ProgressText => IsDownloading ? $"{Progress:F0}%" : string.Empty;

    /// <summary>
    /// Author of the model
    /// </summary>
    public string? Author => Resource.Author;

    /// <summary>
    /// License type
    /// </summary>
    public string? LicenseType => Resource.LicenseType;

    // Determine display name based on context type or filename

    private static string GetDefaultDisplayName(RemoteResource resource)
    {
        // Try to get a friendly name based on the file and context
        var fileName = resource.FileName;

        return resource.ContextType switch
        {
            SharedFolderType.DiffusionModels
                when fileName.Contains("kontext", StringComparison.OrdinalIgnoreCase) => "Flux Kontext UNET",
            SharedFolderType.VAE when fileName.Equals("ae.safetensors", StringComparison.OrdinalIgnoreCase) =>
                "Flux VAE",
            SharedFolderType.TextEncoders
                when fileName.Contains("clip_l", StringComparison.OrdinalIgnoreCase) => "CLIP-L Text Encoder",
            SharedFolderType.TextEncoders
                when fileName.Contains("t5xxl", StringComparison.OrdinalIgnoreCase) => "T5-XXL Text Encoder",
            _ => Path.GetFileNameWithoutExtension(fileName),
        };
    }

    private static string GetTypeBadge(RemoteResource resource)
    {
        return resource.ContextType switch
        {
            SharedFolderType.DiffusionModels => "UNET",
            SharedFolderType.VAE => "VAE",
            SharedFolderType.TextEncoders => "CLIP",
            SharedFolderType.ControlNet => "ControlNet",
            SharedFolderType.Lora or SharedFolderType.LyCORIS => "LoRA",
            _ => resource.ContextType?.ToString() ?? "Model",
        };
    }

    [RelayCommand]
    private void ToggleSelection()
    {
        IsSelected = !IsSelected;
    }
}
