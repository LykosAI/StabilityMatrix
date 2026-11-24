using System;
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

    /// <summary>
    /// Content SHA256 of the file when known (LFS files only), used to detect an already-installed
    /// copy by hash regardless of its name or folder. Null for small non-LFS files (configs etc.).
    /// </summary>
    public string? Sha256 { get; init; }

    public string? ModelsDir { get; init; }

    /// <summary>
    /// Predicate that reports whether a SHA256 is present in the local model index. Lets the row
    /// detect an installed copy by content rather than by path. Null in design mode.
    /// </summary>
    public Func<string, bool>? IsSha256Installed { get; init; }

    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Absolute path this file would be downloaded to under the selected folder, or null in design mode.</summary>
    public string? DestinationPath =>
        ModelsDir == null ? null : System.IO.Path.Combine(ModelsDir, SelectedFolder.ToString(), FileName);

    public string SizeText =>
        Size > 0 ? StabilityMatrix.Core.Helper.Size.FormatBase10Bytes(Size) : string.Empty;

    public string DownloadUrl => $"https://huggingface.co/{RepositoryPath}/resolve/main/{Path}?download=true";

    public IReadOnlyList<SharedFolderType> AvailableFolders => HuggingFaceFolderInference.SelectableFolders;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private SharedFolderType selectedFolder;

    partial void OnSelectedFolderChanged(SharedFolderType value)
    {
        // The on-disk verification was for the previous destination path; a new folder means a new
        // path that may not hold this file. Drop the flag so Exists re-evaluates from scratch (the
        // index-hash match in step 1 is folder-agnostic and still applies).
        verifiedInstalled = false;
        NotifyExistsChanged();
    }

    // File.Exists is a disk syscall, so cache it — recomputed only when the destination
    // folder changes or a download completes (NotifyExistsChanged). Avoids hammering the
    // filesystem on every binding evaluation / selection change in large repos.
    private bool? existsCache;

    public bool Exists => existsCache ??= ComputeExists();

    /// <summary>
    /// Set when a background pass hashed the on-disk file at <see cref="DestinationPath"/> and it
    /// matched this file's <see cref="Sha256"/>. Resolves the case where several repo files share a
    /// name (e.g. high_noise_model.safetensors) but only one variant is actually installed.
    /// </summary>
    private bool verifiedInstalled;

    public void MarkVerifiedInstalled()
    {
        if (verifiedInstalled)
            return;
        verifiedInstalled = true;
        NotifyExistsChanged();
    }

    private bool ComputeExists()
    {
        // 1. Authoritative content match: the file's hash is in the local model index (from a
        //    .cm-info.json sidecar), so it's installed regardless of its name or folder.
        if (Sha256 is { } sha256 && IsSha256Installed?.Invoke(sha256) == true)
            return true;

        // 2. A background pass hashed the on-disk file at our destination and it matched our hash.
        if (verifiedInstalled)
            return true;

        // 3. We have a content hash but it matched nothing on disk or in the index -> not installed,
        //    even if a same-named file exists (it's a different variant). Do NOT fall back to the
        //    name check here, which is what caused unrelated variants to all read as installed.
        if (Sha256 is not null)
            return false;

        // 4. No hash to compare against (small non-LFS files like configs/tokenizers): fall back to
        //    the original name-based check against the destination folder.
        return ModelsDir != null
            && File.Exists(System.IO.Path.Combine(ModelsDir, SelectedFolder.ToString(), FileName));
    }

    public void NotifyExistsChanged()
    {
        existsCache = null;
        OnPropertyChanged(nameof(Exists));
    }
}
