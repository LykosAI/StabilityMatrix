using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Models;

public partial class CheckpointFile : ObservableObject
{
    /// <summary>
    /// Absolute path to the checkpoint file.
    /// </summary>
    [ObservableProperty]
    private string filePath;
    
    /// <summary>
    /// Custom title for UI.
    /// </summary>
    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string? previewImagePath;
    
    [ObservableProperty]
    private BitmapImage? previewImage;
    
    public bool IsPreviewImageLoaded => PreviewImage != null;

    [ObservableProperty]
    private string fileName;

    private static readonly string[] SupportedCheckpointExtensions = { ".safetensors", ".pt" };
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg" };


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
                FileName = file,
            };

            // Check for preview image
            var previewImage = SupportedImageExtensions.Select(ext => $"{checkpointFile.FileName}.preview.{ext}").FirstOrDefault(files.ContainsKey);
            if (previewImage != null)
            {
                checkpointFile.PreviewImage = new BitmapImage(new Uri(Path.Combine(directory, previewImage)));
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
