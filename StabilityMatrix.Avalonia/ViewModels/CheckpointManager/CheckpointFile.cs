using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointManager;

[ManagedService]
[Transient]
public partial class CheckpointFile : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Absolute path to the checkpoint file.
    /// </summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(FileName))]
    private string filePath = string.Empty;

    /// <summary>
    /// Custom title for UI.
    /// </summary>
    [ObservableProperty]
    private string title = string.Empty;

    /// <summary>
    /// Path to preview image. Can be local or remote URL.
    /// </summary>
    [ObservableProperty]
    private string? previewImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectedModel))]
    private ConnectedModelInfo? connectedModel;
    public bool IsConnectedModel => ConnectedModel != null;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private CivitModelType modelType;

    [ObservableProperty]
    private CheckpointFolder parentFolder;

    [ObservableProperty]
    private ProgressReport? progress;

    public string FileName => Path.GetFileName(FilePath);

    public bool CanShowTriggerWords =>
        ConnectedModel != null && !string.IsNullOrWhiteSpace(ConnectedModel.TrainedWordsString);

    public ObservableCollection<string> Badges { get; set; } = new();

    public static readonly string[] SupportedCheckpointExtensions =
    {
        ".safetensors",
        ".pt",
        ".ckpt",
        ".pth",
        ".bin"
    };
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".gif" };
    private static readonly string[] SupportedMetadataExtensions = { ".json" };

    partial void OnConnectedModelChanged(ConnectedModelInfo? value)
    {
        // Update title, first check user defined, then connected model name
        Title = value?.UserTitle ?? value?.ModelName ?? string.Empty;
        // Update badges
        Badges.Clear();
        var fpType = value?.FileMetadata.Fp?.GetStringValue().ToUpperInvariant();
        if (fpType != null)
        {
            Badges.Add(fpType);
        }
        if (!string.IsNullOrWhiteSpace(value?.BaseModel))
        {
            Badges.Add(value.BaseModel);
        }
    }

    private string GetConnectedModelInfoFilePath()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            throw new InvalidOperationException(
                "Cannot get connected model info file path when FilePath is empty"
            );
        }
        var modelNameNoExt = Path.GetFileNameWithoutExtension((string?)FilePath);
        var modelDir = Path.GetDirectoryName((string?)FilePath) ?? "";
        return Path.Combine(modelDir, $"{modelNameNoExt}.cm-info.json");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (File.Exists(FilePath))
        {
            IsLoading = true;
            try
            {
                await using var delay = new MinimumDelay(200, 500);
                await Task.Run(() => File.Delete(FilePath));
                if (PreviewImagePath != null && File.Exists(PreviewImagePath))
                {
                    await Task.Run(() => File.Delete(PreviewImagePath));
                }
                if (ConnectedModel != null)
                {
                    var cmInfoPath = GetConnectedModelInfoFilePath();
                    if (File.Exists(cmInfoPath))
                    {
                        await Task.Run(() => File.Delete(cmInfoPath));
                    }
                }
            }
            catch (IOException ex)
            {
                Logger.Warn($"Failed to delete checkpoint file {FilePath}: {ex.Message}");
                return; // Don't delete from collection
            }
            finally
            {
                IsLoading = false;
            }
        }
        RemoveFromParentList();
    }

    public void OnMoved() => RemoveFromParentList();

    [RelayCommand]
    private async Task RenameAsync()
    {
        // Parent folder path
        var parentPath = Path.GetDirectoryName((string?)FilePath) ?? "";

        var textFields = new TextBoxField[]
        {
            new()
            {
                Label = "File name",
                Validator = text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                        throw new DataValidationException("File name is required");

                    if (File.Exists(Path.Combine(parentPath, text)))
                        throw new DataValidationException("File name already exists");
                },
                Text = FileName
            }
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Rename Model", "", textFields);

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var name = textFields[0].Text;
            var nameNoExt = Path.GetFileNameWithoutExtension(name);
            var originalNameNoExt = Path.GetFileNameWithoutExtension(FileName);
            // Rename file in OS
            try
            {
                var newFilePath = Path.Combine(parentPath, name);
                File.Move(FilePath, newFilePath);
                FilePath = newFilePath;
                // If preview image exists, rename it too
                if (PreviewImagePath != null && File.Exists(PreviewImagePath))
                {
                    var newPreviewImagePath = Path.Combine(
                        parentPath,
                        $"{nameNoExt}.preview{Path.GetExtension((string?)PreviewImagePath)}"
                    );
                    File.Move(PreviewImagePath, newPreviewImagePath);
                    PreviewImagePath = newPreviewImagePath;
                }
                // If connected model info exists, rename it too (<name>.cm-info.json)
                if (ConnectedModel != null)
                {
                    var cmInfoPath = Path.Combine(parentPath, $"{originalNameNoExt}.cm-info.json");
                    if (File.Exists(cmInfoPath))
                    {
                        File.Move(cmInfoPath, Path.Combine(parentPath, $"{nameNoExt}.cm-info.json"));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn(e, $"Failed to rename checkpoint file {FilePath}");
            }
        }
    }

    [RelayCommand]
    private void OpenOnCivitAi()
    {
        if (ConnectedModel?.ModelId == null)
            return;
        ProcessRunner.OpenUrl($"https://civitai.com/models/{ConnectedModel.ModelId}");
    }

    [RelayCommand]
    private Task CopyTriggerWords()
    {
        if (ConnectedModel == null)
            return Task.CompletedTask;

        var words = ConnectedModel.TrainedWordsString;
        if (string.IsNullOrWhiteSpace(words))
            return Task.CompletedTask;

        return App.Clipboard.SetTextAsync(words);
    }

    [RelayCommand]
    private Task CopyModelUrl()
    {
        return ConnectedModel == null
            ? Task.CompletedTask
            : App.Clipboard.SetTextAsync($"https://civitai.com/models/{ConnectedModel.ModelId}");
    }

    [RelayCommand]
    private async Task FindConnectedMetadata(bool forceReimport = false)
    {
        if (
            App.Services.GetService(typeof(IMetadataImportService))
            is not IMetadataImportService importService
        )
            return;

        IsLoading = true;

        try
        {
            var progressReport = new Progress<ProgressReport>(report =>
            {
                Progress = report;
            });

            var cmInfo = await importService.GetMetadataForFile(FilePath, progressReport, forceReimport);
            if (cmInfo != null)
            {
                ConnectedModel = cmInfo;
                PreviewImagePath = SupportedImageExtensions
                    .Select(
                        ext =>
                            Path.Combine(
                                ParentFolder.DirectoryPath,
                                $"{Path.GetFileNameWithoutExtension(FileName)}.preview{ext}"
                            )
                    )
                    .Where(File.Exists)
                    .FirstOrDefault();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Indexes directory and yields all checkpoint files.
    /// First we match all files with supported extensions.
    /// If found, we also look for
    /// - {filename}.preview.{image-extensions} (preview image)
    /// - {filename}.cm-info.json (connected model info)
    /// </summary>
    public static IEnumerable<CheckpointFile> FromDirectoryIndex(
        CheckpointFolder parentFolder,
        string directory,
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    )
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.*", searchOption))
        {
            if (
                !SupportedCheckpointExtensions.Any(
                    ext => Path.GetExtension(file).Equals(ext, StringComparison.InvariantCultureIgnoreCase)
                )
            )
                continue;

            var checkpointFile = new CheckpointFile
            {
                Title = Path.GetFileNameWithoutExtension(file),
                FilePath = Path.Combine(directory, file),
            };

            var jsonPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(file)}.cm-info.json");
            if (File.Exists(jsonPath))
            {
                var json = File.ReadAllText(jsonPath);
                var connectedModelInfo = ConnectedModelInfo.FromJson(json);
                checkpointFile.ConnectedModel = connectedModelInfo;
            }

            checkpointFile.PreviewImagePath = SupportedImageExtensions
                .Select(
                    ext => Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(file)}.preview{ext}")
                )
                .Where(File.Exists)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(checkpointFile.PreviewImagePath))
            {
                checkpointFile.PreviewImagePath = Assets.NoImage.ToString();
            }

            checkpointFile.ParentFolder = parentFolder;

            yield return checkpointFile;
        }
    }

    public static IEnumerable<CheckpointFile> GetAllCheckpointFiles(string modelsDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(modelsDirectory, "*.*", SearchOption.AllDirectories))
        {
            if (
                !SupportedCheckpointExtensions.Any(
                    ext => Path.GetExtension(file).Equals(ext, StringComparison.InvariantCultureIgnoreCase)
                )
            )
                continue;

            var checkpointFile = new CheckpointFile
            {
                Title = Path.GetFileNameWithoutExtension(file),
                FilePath = file
            };

            var jsonPath = Path.Combine(
                Path.GetDirectoryName(file) ?? "",
                Path.GetFileNameWithoutExtension(file) + ".cm-info.json"
            );

            if (File.Exists(jsonPath))
            {
                var json = File.ReadAllText(jsonPath);
                var connectedModelInfo = ConnectedModelInfo.FromJson(json);
                checkpointFile.ConnectedModel = connectedModelInfo;
                checkpointFile.ModelType = GetCivitModelType(file);
            }

            checkpointFile.PreviewImagePath = SupportedImageExtensions
                .Select(
                    ext =>
                        Path.Combine(
                            Path.GetDirectoryName(file) ?? "",
                            $"{Path.GetFileNameWithoutExtension(file)}.preview{ext}"
                        )
                )
                .Where(File.Exists)
                .FirstOrDefault();

            yield return checkpointFile;
        }
    }

    /// <summary>
    /// Index with progress reporting.
    /// </summary>
    public static IEnumerable<CheckpointFile> FromDirectoryIndex(
        CheckpointFolder parentFolder,
        string directory,
        IProgress<ProgressReport> progress,
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    )
    {
        var current = 0ul;
        foreach (var checkpointFile in FromDirectoryIndex(parentFolder, directory, searchOption))
        {
            current++;
            progress.Report(new ProgressReport(current, "Indexing", checkpointFile.FileName));
            yield return checkpointFile;
        }
    }

    private static CivitModelType GetCivitModelType(string filePath)
    {
        if (filePath.Contains(SharedFolderType.StableDiffusion.ToString()))
        {
            return CivitModelType.Checkpoint;
        }

        if (filePath.Contains(SharedFolderType.ControlNet.ToString()))
        {
            return CivitModelType.Controlnet;
        }

        if (filePath.Contains(SharedFolderType.Lora.ToString()))
        {
            return CivitModelType.LORA;
        }

        if (filePath.Contains(SharedFolderType.TextualInversion.ToString()))
        {
            return CivitModelType.TextualInversion;
        }

        if (filePath.Contains(SharedFolderType.Hypernetwork.ToString()))
        {
            return CivitModelType.Hypernetwork;
        }

        if (filePath.Contains(SharedFolderType.LyCORIS.ToString()))
        {
            return CivitModelType.LoCon;
        }

        return CivitModelType.Unknown;
    }

    private sealed class FilePathEqualityComparer : IEqualityComparer<CheckpointFile>
    {
        public bool Equals(CheckpointFile? x, CheckpointFile? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (ReferenceEquals(x, null))
                return false;
            if (ReferenceEquals(y, null))
                return false;
            if (x.GetType() != y.GetType())
                return false;
            return x.FilePath == y.FilePath
                && x.ConnectedModel?.Hashes.BLAKE3 == y.ConnectedModel?.Hashes.BLAKE3
                && x.ConnectedModel?.ThumbnailImageUrl == y.ConnectedModel?.ThumbnailImageUrl
                && x.PreviewImagePath == y.PreviewImagePath;
        }

        public int GetHashCode(CheckpointFile obj)
        {
            return obj.FilePath.GetHashCode();
        }
    }

    public static IEqualityComparer<CheckpointFile> FilePathComparer { get; } =
        new FilePathEqualityComparer();
}
