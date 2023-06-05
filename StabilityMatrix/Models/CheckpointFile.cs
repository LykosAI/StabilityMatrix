using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StabilityMatrix.Models;

public partial class CheckpointFile : ObservableObject
{
    // Event for when this file is deleted
    public event EventHandler<CheckpointFile>? Deleted;
    
    /// <summary>
    /// Absolute path to the checkpoint file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;
    
    /// <summary>
    /// Custom title for UI.
    /// </summary>
    public string Title { get; init; } = string.Empty;
    
    public string? PreviewImagePath { get; set; } 
    
    public BitmapImage? PreviewImage { get; set; }
    
    public bool IsPreviewImageLoaded => PreviewImage != null;
    
    public string FileName => Path.GetFileName(FilePath);

    private static readonly string[] SupportedCheckpointExtensions = { ".safetensors", ".pt", ".ckpt", ".pth" };
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg" };

    [RelayCommand]
    public void Delete()
    {
        if (File.Exists(FilePath))
        {
            Task.Run(() =>
            {
                File.Delete(FilePath);
                Deleted?.Invoke(this, this);
            });
        }

        if (PreviewImagePath != null && File.Exists(PreviewImagePath))
        {
            Task.Run(() => File.Delete(PreviewImagePath));
        }
    } 

    /// <summary>
    /// Indexes directory and yields all checkpoint files.
    /// First we match all files with supported extensions.
    /// If found, we also look for
    /// - {filename}.preview.{image-extensions}
    /// </summary>
    public static IEnumerable<CheckpointFile> FromDirectoryIndex(string directory, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        // Get all files with supported extensions
        var allExtensions = SupportedCheckpointExtensions.Concat(SupportedImageExtensions).ToImmutableHashSet();

        var files = allExtensions.AsParallel()
            .SelectMany(pattern => Directory.EnumerateFiles(directory, $"*{pattern}", searchOption)).ToDictionary<string, string>(Path.GetFileName);

        foreach (var file in files.Keys.Where(k => SupportedCheckpointExtensions.Contains(Path.GetExtension(k))))
        {
            var checkpointFile = new CheckpointFile
            {
                Title = Path.GetFileNameWithoutExtension(file),
                FilePath = Path.Combine(directory, file),
            };

            // Check for preview image
            var previewImage = SupportedImageExtensions.Select(ext => $"{checkpointFile.FileName}.preview.{ext}").FirstOrDefault(files.ContainsKey);
            if (previewImage != null)
            {
                checkpointFile.PreviewImagePath = Path.Combine(directory, previewImage);
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
