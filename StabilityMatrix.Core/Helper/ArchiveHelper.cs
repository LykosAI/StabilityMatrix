using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using SharpCompress.Common;
using SharpCompress.Readers;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using Timer = System.Timers.Timer;

namespace StabilityMatrix.Core.Helper;

public record struct ArchiveInfo(ulong Size, ulong CompressedSize);

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static partial class ArchiveHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Platform-specific 7z executable name.
    /// </summary>
    public static string SevenZipFileName
    {
        get
        {
            if (Compat.IsWindows)
            {
                return "7za.exe";
            }
            if (Compat.IsLinux)
            {
                return "7zzs";
            }
            if (Compat.IsMacOS)
            {
                return "7zz";
            }
            throw new PlatformNotSupportedException("7z is not supported on this platform.");
        }
    }

    // HomeDir is set by ISettingsManager.TryFindLibrary()
    public static string HomeDir { get; set; } = string.Empty;

    public static string SevenZipPath => Path.Combine(HomeDir, "Assets", SevenZipFileName);

    [GeneratedRegex(@"(?<=Size:\s*)\d+|(?<=Compressed:\s*)\d+")]
    private static partial Regex Regex7ZOutput();

    [GeneratedRegex(@"(?<=\s*)\d+(?=%)")]
    private static partial Regex Regex7ZProgressDigits();

    [GeneratedRegex(@"(\d+)%.*- (.*)")]
    private static partial Regex Regex7ZProgressFull();

    public static async Task<ArchiveInfo> TestArchive(string archivePath)
    {
        var process = ProcessRunner.StartAnsiProcess(SevenZipPath, new[] { "t", archivePath });
        await process.WaitForExitAsync().ConfigureAwait(false);
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var matches = Regex7ZOutput().Matches(output);
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

        var result = await ProcessRunner
            .GetProcessResultAsync(
                SevenZipPath,
                new[] { "a", archivePath, sourceDirName + @"\", "-y" },
                workingDirectory: sourceParent
            )
            .ConfigureAwait(false);
        result.EnsureSuccessExitCode();
    }

    public static async Task<ArchiveInfo> Extract7Z(string archivePath, string extractDirectory)
    {
        var args = $"x {ProcessRunner.Quote(archivePath)} -o{ProcessRunner.Quote(extractDirectory)} -y";

        var result = await ProcessRunner
            .GetProcessResultAsync(SevenZipPath, args)
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);

        var output = result.StandardOutput ?? "";

        try
        {
            var matches = Regex7ZOutput().Matches(output);
            var size = ulong.Parse(matches[0].Value);
            var compressed = ulong.Parse(matches[1].Value);
            return new ArchiveInfo(size, compressed);
        }
        catch (Exception e)
        {
            throw new Exception($"Could not parse 7z output [{e.Message}]: {output.ToRepr()}");
        }
    }

    public static async Task<ArchiveInfo> Extract7Z(
        string archivePath,
        string extractDirectory,
        IProgress<ProgressReport> progress
    )
    {
        var outputStore = new StringBuilder();
        var onOutput = new Action<string?>(s =>
        {
            if (s == null)
                return;

            // Parse progress
            Logger.Trace($"7z: {s}");
            outputStore.AppendLine(s);
            var match = Regex7ZProgressFull().Match(s);
            if (match.Success)
            {
                var percent = int.Parse(match.Groups[1].Value);
                var currentFile = match.Groups[2].Value;
                progress.Report(
                    new ProgressReport(
                        percent / (float)100,
                        "Extracting",
                        currentFile,
                        type: ProgressType.Extract
                    )
                );
            }
        });
        progress.Report(new ProgressReport(-1, isIndeterminate: true, type: ProgressType.Extract));

        // Need -bsp1 for progress reports
        var args = $"x {ProcessRunner.Quote(archivePath)} -o{ProcessRunner.Quote(extractDirectory)} -y -bsp1";
        Logger.Debug($"Starting process '{SevenZipPath}' with arguments '{args}'");

        using var process = ProcessRunner.StartProcess(SevenZipPath, args, outputDataReceived: onOutput);

        await process.WaitForExitAsync().ConfigureAwait(false);

        ProcessException.ThrowIfNonZeroExitCode(process, outputStore);

        progress.Report(new ProgressReport(1f, "Finished extracting", type: ProgressType.Extract));

        var output = outputStore.ToString();

        try
        {
            var matches = Regex7ZOutput().Matches(output);
            var size = ulong.Parse(matches[0].Value);
            var compressed = ulong.Parse(matches[1].Value);
            return new ArchiveInfo(size, compressed);
        }
        catch (Exception e)
        {
            throw new Exception($"Could not parse 7z output [{e.Message}]: {output.ToRepr()}");
        }
    }

    /// <summary>
    /// Extracts a zipped tar (i.e. '.tar.gz') archive.
    /// First extracts the zipped tar, then extracts the tar and removes the tar.
    /// </summary>
    /// <param name="archivePath"></param>
    /// <param name="extractDirectory"></param>
    /// <returns></returns>
    public static async Task<ArchiveInfo> Extract7ZTar(string archivePath, string extractDirectory)
    {
        if (!archivePath.EndsWith(".tar.gz"))
        {
            throw new ArgumentException("Archive must be a zipped tar.");
        }
        // Extract the tar.gz to tar
        await Extract7Z(archivePath, extractDirectory).ConfigureAwait(false);

        // Extract the tar
        var tarPath = Path.Combine(extractDirectory, Path.GetFileNameWithoutExtension(archivePath));
        if (!File.Exists(tarPath))
        {
            throw new FileNotFoundException("Tar file not found.", tarPath);
        }

        try
        {
            return await Extract7Z(tarPath, extractDirectory).ConfigureAwait(false);
        }
        finally
        {
            // Remove the tar
            if (File.Exists(tarPath))
            {
                File.Delete(tarPath);
            }
        }
    }

    /// <summary>
    /// Extracts with auto handling of tar.gz files.
    /// </summary>
    public static async Task<ArchiveInfo> Extract7ZAuto(string archivePath, string extractDirectory)
    {
        if (archivePath.EndsWith(".tar.gz"))
        {
            return await Extract7ZTar(archivePath, extractDirectory).ConfigureAwait(false);
        }
        else
        {
            return await Extract7Z(archivePath, extractDirectory).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Extract an archive to the output directory.
    /// </summary>
    /// <param name="progress"></param>
    /// <param name="archivePath"></param>
    /// <param name="outputDirectory">Output directory, created if does not exist.</param>
    public static async Task Extract(
        string archivePath,
        string outputDirectory,
        IProgress<ProgressReport>? progress = default
    )
    {
        Directory.CreateDirectory(outputDirectory);
        progress?.Report(new ProgressReport(-1, isIndeterminate: true));

        var count = 0ul;

        // Get true size
        var (total, _) = await TestArchive(archivePath).ConfigureAwait(false);

        // If not available, use the size of the archive file
        if (total == 0)
        {
            total = (ulong)new FileInfo(archivePath).Length;
        }

        // Create an DispatchTimer that monitors the progress of the extraction
        var progressMonitor = progress switch
        {
            null => null,
            _ => new Timer(TimeSpan.FromMilliseconds(36))
        };

        if (progressMonitor != null)
        {
            progressMonitor.Elapsed += (_, _) =>
            {
                if (count == 0)
                    return;
                progress!.Report(new ProgressReport(count, total, message: "Extracting"));
            };
        }

        await Task.Factory.StartNew(
            () =>
            {
                var extractOptions = new ExtractionOptions { Overwrite = true, ExtractFullPath = true, };
                using var stream = File.OpenRead(archivePath);
                using var archive = ReaderFactory.Open(stream);

                // Start the progress reporting timer
                progressMonitor?.Start();

                while (archive.MoveToNextEntry())
                {
                    var entry = archive.Entry;
                    if (!entry.IsDirectory)
                    {
                        count += (ulong)entry.CompressedSize;
                    }
                    archive.WriteEntryToDirectory(outputDirectory, extractOptions);
                }
            },
            TaskCreationOptions.LongRunning
        )
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(progress: 1, message: "Done extracting"));
        progressMonitor?.Stop();
        Logger.Info("Finished extracting archive {}", archivePath);
    }

    /// <summary>
    /// Extract an archive to the output directory, using SharpCompress managed code.
    /// does not require 7z to be installed, but no progress reporting.
    /// </summary>
    public static async Task ExtractManaged(string archivePath, string outputDirectory)
    {
        await using var stream = File.OpenRead(archivePath);
        await ExtractManaged(stream, outputDirectory).ConfigureAwait(false);
    }

    /// <summary>
    /// Extract an archive to the output directory, using SharpCompress managed code.
    /// does not require 7z to be installed, but no progress reporting.
    /// </summary>
    public static async Task ExtractManaged(Stream stream, string outputDirectory)
    {
        var fullOutputDir = Path.GetFullPath(outputDirectory);
        using var reader = ReaderFactory.Open(stream);
        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            var outputPath = Path.Combine(outputDirectory, entry.Key);

            if (entry.IsDirectory)
            {
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }
            }
            else
            {
                var folder = Path.GetDirectoryName(entry.Key)!;
                var destDir = Path.GetFullPath(Path.Combine(fullOutputDir, folder));

                if (!Directory.Exists(destDir))
                {
                    if (!destDir.StartsWith(fullOutputDir, StringComparison.Ordinal))
                    {
                        throw new ExtractionException(
                            "Entry is trying to create a directory outside of the destination directory."
                        );
                    }

                    Directory.CreateDirectory(destDir);
                }

                // Check if symbolic link
                if (entry.LinkTarget != null)
                {
                    // Not sure why but symlink entries have a key that ends with a space
                    // and some broken path suffix, so we'll remove everything after the last space
                    Logger.Debug(
                        $"Checking if output path {outputPath} contains space char: {outputPath.Contains(' ')}"
                    );
                    if (outputPath.Contains(' '))
                    {
                        outputPath = outputPath[..outputPath.LastIndexOf(' ')];
                    }

                    Logger.Debug(
                        $"Extracting symbolic link [{entry.Key.ToRepr()}] "
                            + $"({outputPath.ToRepr()} to {entry.LinkTarget.ToRepr()})"
                    );
                    // Try to write link, if fail, continue copy file
                    try
                    {
                        // Delete path if exists
                        File.Delete(outputPath);
                        File.CreateSymbolicLink(outputPath, entry.LinkTarget);
                        continue;
                    }
                    catch (IOException e)
                    {
                        Logger.Warn($"Could not extract symbolic link, copying file instead: {e.Message}");
                    }
                }

                // Write file
                await using var entryStream = reader.OpenEntryStream();
                await using var fileStream = File.Create(outputPath);
                await entryStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
        }
    }

    [SupportedOSPlatform("macos")]
    public static async Task ExtractDmg(string archivePath, DirectoryPath extractDir)
    {
        using var mountPoint = new TempDirectoryPath();

        // Mount the dmg
        await ProcessRunner
            .GetProcessResultAsync("hdiutil", ["attach", archivePath, "-mountpoint", mountPoint.FullPath])
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);

        try
        {
            // Copy apps
            foreach (var sourceDir in mountPoint.EnumerateDirectories("*.app"))
            {
                var destDir = extractDir.JoinDir(sourceDir.RelativeTo(mountPoint));

                await ProcessRunner
                    .GetProcessResultAsync("cp", ["-R", sourceDir.FullPath, destDir.FullPath])
                    .EnsureSuccessExitCode()
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            // Unmount the dmg
            await ProcessRunner
                .GetProcessResultAsync("hdiutil", ["detach", mountPoint.FullPath])
                .ConfigureAwait(false);
        }
    }
}
