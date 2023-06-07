using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;

namespace StabilityMatrix.Models;

public partial class CheckpointFile : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    // Event for when this file is deleted
    public event EventHandler<CheckpointFile>? Deleted;

    /// <summary>
    /// Absolute path to the checkpoint file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Custom title for UI.
    /// </summary>
    [ObservableProperty] private string title = string.Empty;
    
    public string? PreviewImagePath { get; set; }
    public BitmapImage? PreviewImage { get; set; }
    public bool IsPreviewImageLoaded => PreviewImage != null;

    [ObservableProperty] private ConnectedModelInfo? connectedModel;
    public bool IsConnectedModel => ConnectedModel != null;

    [ObservableProperty] private bool isLoading;
    
    public string FileName => Path.GetFileName(FilePath);

    private static readonly string[] SupportedCheckpointExtensions = { ".safetensors", ".pt", ".ckpt", ".pth", "bin" };
    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg" };
    private static readonly string[] SupportedMetadataExtensions = { ".json" };

    partial void OnConnectedModelChanged(ConnectedModelInfo? value)
    {
        if (value == null) return;
        // Update title, first check user defined, then connected model name
        Title = value.UserTitle ?? value.ModelName;
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (File.Exists(FilePath))
        {
            // Start progress ring
            IsLoading = true;
            var timer = Stopwatch.StartNew();
            try
            {
                await Task.Run(() => File.Delete(FilePath));
                if (PreviewImagePath != null && File.Exists(PreviewImagePath))
                {
                    await Task.Run(() => File.Delete(PreviewImagePath));
                }
                // If it was too fast, wait a bit to show progress ring
                var targetDelay = new Random().Next(200, 500);
                var elapsed = timer.ElapsedMilliseconds;
                if (elapsed < targetDelay)
                {
                    await Task.Delay(targetDelay - (int) elapsed);
                }
            }
            catch (IOException e)
            {
                Logger.Error(e, $"Failed to delete checkpoint file: {FilePath}");
                IsLoading = false;
                return; // Don't delete from collection
            }
        }
        Deleted?.Invoke(this, this);
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
            .Concat(SupportedMetadataExtensions)
            .ToImmutableHashSet();

        var files = allExtensions.AsParallel()
            .SelectMany(pattern => Directory.EnumerateFiles(directory, $"*{pattern}", searchOption)).ToDictionary<string, string>(Path.GetFileName);

        foreach (var file in files.Keys.Where(k => SupportedCheckpointExtensions.Contains(Path.GetExtension(k))))
        {
            var checkpointFile = new CheckpointFile
            {
                Title = Path.GetFileNameWithoutExtension(file),
                FilePath = Path.Combine(directory, file),
            };
            
            // Check for connected model info
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var cmInfoPath = $"{fileNameWithoutExtension}.cm-info.json";
            if (files.ContainsKey(cmInfoPath))
            {
                try
                {
                    var jsonData = File.ReadAllText(Path.Combine(directory, cmInfoPath));
                    checkpointFile.ConnectedModel = ConnectedModelInfo.FromJson(jsonData);
                }
                catch (IOException e)
                {
                    Debug.WriteLine($"Failed to parse {cmInfoPath}: {e}");
                }
            }

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
