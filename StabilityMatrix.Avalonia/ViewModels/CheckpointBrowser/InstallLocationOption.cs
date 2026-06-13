using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointBrowser;

/// <summary>
/// A download target shown in the CivArchive download split-button flyout.
/// <see cref="Directory"/> is null for the "Custom..." entry, which prompts the user
/// to pick a folder instead of using a known models subdirectory.
/// </summary>
public record InstallLocationOption(string DisplayName, DirectoryPath? Directory);
