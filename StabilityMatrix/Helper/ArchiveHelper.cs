using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using ICSharpCode.SharpZipLib.BZip2;
using NLog;
using SharpCompress.Common;
using SharpCompress.Readers;
using StabilityMatrix.Models;

namespace StabilityMatrix.Helper;

public record struct ArchiveInfo(ulong Size, ulong CompressedSize);

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class ArchiveHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string RelativeSevenZipPath = @"Assets\7za.exe";
    public static string SevenZipPath => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeSevenZipPath));
    
    private static readonly Regex Regex7ZOutput = new(@"(?<=Size:\s*)\d+|(?<=Compressed:\s*)\d+");

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

    private static ulong GetSizeBZip2(string archivePath)
    {
        using var countStream = File.OpenRead(archivePath); 
        using var bzipStream = new BZip2InputStream(countStream);
        bzipStream.IsStreamOwner = true;
        
        var readBytes = 0ul;
        while (true)
        {
            var result = bzipStream.ReadByte();
            if (result == -1) break;
            readBytes++;
        }
        
        return readBytes;
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
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        // Get true size
        var (total, compressed) = await TestArchive(archivePath);

        // If not available, use the size of the archive file
        if (total == 0)
        {
            total = (ulong) new FileInfo(archivePath).Length;
        }

        // Total read is the
        stopwatch.Stop();
        Logger.Info("Read archive size in {} s", stopwatch.Elapsed.TotalSeconds);
        stopwatch.Restart();
        
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
                progress!.Report(new ProgressReport(count, total));
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
        
        progress?.Report(new ProgressReport(progress: 1));
        
        stopwatch.Stop();
        Logger.Info("Finished extracting archive {} in {}s", archivePath, stopwatch.Elapsed.TotalSeconds);
    }
}
