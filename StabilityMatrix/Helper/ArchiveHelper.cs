using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NLog;
using SharpCompress.Common;
using SharpCompress.Readers;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Helper;

public record struct ArchiveInfo(ulong Size, ulong CompressedSize);

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class ArchiveHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // HomeDir is set by ISettingsManager.TryFindLibrary()
    public static string HomeDir { get; set; } = string.Empty;
    public static string SevenZipPath => Path.Combine(HomeDir, "Assets", "7za.exe");
    
    private static readonly Regex Regex7ZOutput = new(@"(?<=Size:\s*)\d+|(?<=Compressed:\s*)\d+");
    private static readonly Regex Regex7ZProgressDigits = new(@"(?<=\s*)\d+(?=%)");
    private static readonly Regex Regex7ZProgressFull = new(@"(\d+)%.*- (.*)");
    
    public static async Task<ArchiveInfo> TestArchive(string archivePath)
    {
        var process = ProcessRunner.StartProcess(SevenZipPath, new[] {"t", archivePath});
        await process.WaitForExitAsync();
        var output = await process.StandardOutput.ReadToEndAsync();
        var matches = Regex7ZOutput.Matches(output);
        var size = ulong.Parse(matches[0].Value);
        var compressed = ulong.Parse(matches[1].Value);
        return new ArchiveInfo(size, compressed);
    }

    public static async Task AddToArchive7Z(string archivePath, string sourceDirectory)
    {
        // Start 7z in the parent directory of the source directory
        var sourceParent = Directory.GetParent(sourceDirectory)?.FullName ?? "";
        // We must pass in as `directory\` for archive path to be correct
        var sourceDirName = new DirectoryInfo(sourceDirectory).Name;
        var process = ProcessRunner.StartProcess(SevenZipPath, new[]
        {
            "a", archivePath, sourceDirName + @"\", "-y"
        }, workingDirectory: sourceParent);
        await ProcessRunner.WaitForExitConditionAsync(process);
    }
    
    public static async Task<ArchiveInfo> Extract7Z(string archivePath, string extractDirectory)
    {
        var args =
            $"x {ProcessRunner.Quote(archivePath)} -o{ProcessRunner.Quote(extractDirectory)} -y";
        var process = ProcessRunner.StartProcess(SevenZipPath, args);
        await ProcessRunner.WaitForExitConditionAsync(process);
        var output = await process.StandardOutput.ReadToEndAsync();
        var matches = Regex7ZOutput.Matches(output);
        var size = ulong.Parse(matches[0].Value);
        var compressed = ulong.Parse(matches[1].Value);
        return new ArchiveInfo(size, compressed);
    }
    
    public static async Task<ArchiveInfo> Extract7Z(string archivePath, string extractDirectory, IProgress<ProgressReport> progress)
    {
        var outputStore = new StringBuilder();
        var onOutput = new Action<ProcessOutput>(s =>
        {
            // Parse progress
            Logger.Trace($"7z: {s}");
            outputStore.AppendLine(s.Text);
            var match = Regex7ZProgressFull.Match(s.Text ?? "");
            if (match.Success)
            {
                var percent = int.Parse(match.Groups[1].Value);
                var currentFile = match.Groups[2].Value;
                progress.Report(new ProgressReport(percent / (float) 100, "Extracting", currentFile, type: ProgressType.Extract));
            }
        });
        progress.Report(new ProgressReport(-1, isIndeterminate: true, type: ProgressType.Extract));
        
        // Need -bsp1 for progress reports
        var args =
            $"x {ProcessRunner.Quote(archivePath)} -o{ProcessRunner.Quote(extractDirectory)} -y -bsp1";
        var process = ProcessRunner.StartProcess(SevenZipPath, args, outputDataReceived: onOutput);
        
        await process.WaitForExitAsync();
        
        progress.Report(new ProgressReport(1, "Finished extracting", type: ProgressType.Extract));
        
        var output = outputStore.ToString();
        var matches = Regex7ZOutput.Matches(output);
        var size = ulong.Parse(matches[0].Value);
        var compressed = ulong.Parse(matches[1].Value);
        return new ArchiveInfo(size, compressed);
    }

    /// <summary>
    /// Extract an archive to the output directory.
    /// </summary>
    /// <param name="progress"></param>
    /// <param name="archivePath"></param>
    /// <param name="outputDirectory">Output directory, created if does not exist.</param>
    public static async Task Extract(string archivePath, string outputDirectory, IProgress<ProgressReport>? progress = default)
    {
        Directory.CreateDirectory(outputDirectory);
        progress?.Report(new ProgressReport(-1, isIndeterminate: true));

        var count = 0ul;

        // Get true size
        var (total, _) = await TestArchive(archivePath);

        // If not available, use the size of the archive file
        if (total == 0)
        {
            total = (ulong) new FileInfo(archivePath).Length;
        }

        // Create an DispatchTimer that monitors the progress of the extraction
        var progressMonitor = progress switch {
            null => null,
            _ => new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(36) }
        };
        if (progressMonitor != null)
        {
            progressMonitor.Tick += (sender, args) =>
            {
                // Ignore 0 counts
                if (count == 0) return;
                Application.Current.Dispatcher.Invoke(() => progress!.Report(new ProgressReport(count, total, message: "Extracting")));
            };
        }

        await Task.Factory.StartNew(() =>
        {
            var extractOptions = new ExtractionOptions
            {
                Overwrite = true,
                ExtractFullPath = true,
            };
            using var stream = File.OpenRead(archivePath);
            using var archive = ReaderFactory.Open(stream);

            // Start the progress reporting timer
            progressMonitor?.Start();
            
            while (archive.MoveToNextEntry())
            {
                var entry = archive.Entry;
                if (!entry.IsDirectory)
                {
                    count += (ulong) entry.CompressedSize;
                }
                archive.WriteEntryToDirectory(outputDirectory, extractOptions);
            }
        }, TaskCreationOptions.LongRunning);
        
        progress?.Report(new ProgressReport(progress: 1, message: "Done extracting"));
        progressMonitor?.Stop();
        Logger.Info("Finished extracting archive {}", archivePath);
    }
}
