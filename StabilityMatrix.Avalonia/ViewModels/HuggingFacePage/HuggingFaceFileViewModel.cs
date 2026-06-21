using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Models.HuggingFace;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;

/// <summary>
/// A single file row in the live HuggingFace repo browser, with a checkbox and an
/// editable destination folder.
/// </summary>
public partial class HuggingFaceFileViewModel : ViewModelBase
{
    /// <summary>Path of the file relative to the repo root.</summary>
    public required string Path { get; init; }

    /// <summary>Full repo id (<c>owner/name</c>) this file belongs to.</summary>
    public required string RepositoryPath { get; init; }

    public required long Size { get; init; }

    public string? ModelsDir { get; init; }

    public string FileName => System.IO.Path.GetFileName(Path);

    public string SizeText =>
        Size > 0 ? StabilityMatrix.Core.Helper.Size.FormatBase10Bytes(Size) : string.Empty;

    public string DownloadUrl => $"https://huggingface.co/{RepositoryPath}/resolve/main/{Path}?download=true";

    public IReadOnlyList<SharedFolderType> AvailableFolders => HuggingFaceFolderInference.SelectableFolders;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private SharedFolderType selectedFolder;

    partial void OnSelectedFolderChanged(SharedFolderType value) => NotifyExistsChanged();

    // File.Exists is a disk syscall, so cache it — recomputed only when the destination
    // folder changes or a download completes (NotifyExistsChanged). Avoids hammering the
    // filesystem on every binding evaluation / selection change in large repos.
    private bool? existsCache;

    public bool Exists => existsCache ??= ComputeExists();

    private bool ComputeExists() =>
        ModelsDir != null
        && File.Exists(System.IO.Path.Combine(ModelsDir, SelectedFolder.ToString(), FileName));

    public void NotifyExistsChanged()
    {
        existsCache = null;
        OnPropertyChanged(nameof(Exists));
    }
}
