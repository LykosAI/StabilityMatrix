using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Avalonia.ViewModels.OutputsPage;

public partial class OutputImageViewModel(LocalImageFile imageFile) : SelectableViewModelBase
{
    public LocalImageFile ImageFile { get; } = imageFile;

    /// <summary>
    /// Thumbnail path for video files. Set asynchronously after thumbnail generation.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayPath))]
    public partial string? ThumbnailPath { get; set; }

    /// <summary>
    /// Path to display - uses thumbnail for videos, original path for images.
    /// </summary>
    public string DisplayPath =>
        ImageFile.IsVideo && ThumbnailPath != null ? ThumbnailPath : ImageFile.AbsolutePath;
}
