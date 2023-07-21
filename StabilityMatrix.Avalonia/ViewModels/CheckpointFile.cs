using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Data;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels;

public partial class CheckpointFile : ViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    // Event for when this file is deleted
    public event EventHandler<CheckpointFile>? Deleted;

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
    
    public string? PreviewImagePath { get; set; }
    public Bitmap? PreviewImage { get; set; }
    public bool IsPreviewImageLoaded => PreviewImage != null;

    [ObservableProperty]
    private ConnectedModelInfo? connectedModel;
    public bool IsConnectedModel => ConnectedModel != null;

    [ObservableProperty] private bool isLoading;
    
    public string FileName => Path.GetFileName(FilePath);

    public ObservableCollection<string> Badges { get; set; } = new();

    private static readonly string[] SupportedCheckpointExtensions = { ".safetensors", ".pt", ".ckpt", ".pth", "bin" };
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg" };
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
        Deleted?.Invoke(this, this);
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        // Parent folder path
        var parentPath = Path.GetDirectoryName(FilePath) ?? "";

        var textFields = new TextBoxField[]
        {
            new()
            {
                Label = "File name",
                Validator = text =>
                {
                    if (string.IsNullOrWhiteSpace(text)) throw new 
                        DataValidationException("File name is required");
                    
                    if (File.Exists(Path.Combine(parentPath, text))) throw new 
                        DataValidationException("File name already exists");
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
                    var newPreviewImagePath = Path.Combine(parentPath, $"{nameNoExt}.preview{Path.GetExtension(PreviewImagePath)}");
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
        if (ConnectedModel?.ModelId == null) return;
        ProcessRunner.OpenUrl($"https://civitai.com/models/{ConnectedModel.ModelId}");
    }
    
    // Loads image from path
    private async Task LoadPreviewImage()
    {
        if (PreviewImagePath == null) return;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            PreviewImage = new Bitmap(File.OpenRead(PreviewImagePath));
        });
    }

    /// <summary>
    /// Indexes directory and yields all checkpoint files.
    /// First we match all files with supported extensions.
    /// If found, we also look for
    /// - {filename}.preview.{image-extensions} (preview image)
    /// - {filename}.cm-info.json (connected model info)
    /// </summary>
    public static IEnumerable<CheckpointFile> FromDirectoryIndex(string directory, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        // Get all files with supported extensions
        var allExtensions = SupportedCheckpointExtensions
            .Concat(SupportedImageExtensions)
            .Concat(SupportedMetadataExtensions);

        var files = allExtensions.AsParallel()
            .SelectMany(pattern => Directory.EnumerateFiles(directory, $"*{pattern}", searchOption)).ToDictionary<string, string>(Path.GetFileName);

        foreach (var file in files.Keys.Where(k => SupportedCheckpointExtensions.Contains(Path.GetExtension(k))))
        {
            var checkpointFile = new CheckpointFile()
            {
                Title = Path.GetFileNameWithoutExtension(file),
                FilePath = Path.Combine(directory, file),
            };
            
            // Check for connected model info
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var cmInfoPath = $"{fileNameWithoutExtension}.cm-info.json";
            if (files.TryGetValue(cmInfoPath, out var jsonPath))
            {
                try
                {
                    var jsonData = File.ReadAllText(jsonPath);
                    checkpointFile.ConnectedModel = ConnectedModelInfo.FromJson(jsonData);
                }
                catch (IOException e)
                {
                    Debug.WriteLine($"Failed to parse {cmInfoPath}: {e}");
                }
            }

            // Check for preview image
            var previewImage = SupportedImageExtensions.Select(ext => $"{fileNameWithoutExtension}.preview{ext}").FirstOrDefault(files.ContainsKey);
            if (previewImage != null)
            {
                checkpointFile.PreviewImagePath = files[previewImage];
                checkpointFile.LoadPreviewImage().SafeFireAndForget();
            }

            yield return checkpointFile;
        }
    }

    /// <summary>
    /// Index with progress reporting.
    /// </summary>
    public static IEnumerable<CheckpointFile> FromDirectoryIndex(string directory, IProgress<ProgressReport> progress,
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var current = 0ul;
        foreach (var checkpointFile in FromDirectoryIndex(directory, searchOption))
        {
            current++;
            progress.Report(new ProgressReport(current, "Indexing", checkpointFile.FileName));
            yield return checkpointFile;
        }
    }
}
