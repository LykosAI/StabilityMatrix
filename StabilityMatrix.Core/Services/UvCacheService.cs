using System.Diagnostics;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Services;

public interface IUvCacheService
{
    /// <summary>
    /// The effective uv cache directory, respecting a user-defined UV_CACHE_DIR override.
    /// </summary>
    DirectoryPath GetCacheDirectory();

    /// <summary>
    /// Total size of the uv cache in bytes, or 0 if the cache directory does not exist.
    /// </summary>
    Task<long> GetSizeBytesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the uv cache via <c>uv cache clean</c> (safe against concurrent uv use,
    /// unlike deleting the directory). Reports percentage progress by watching the cache
    /// shrink. Returns the number of bytes freed; throws on a failed uv process.
    /// </summary>
    Task<long> PurgeAsync(
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
}

[RegisterSingleton<IUvCacheService, UvCacheService>]
public class UvCacheService(ISettingsManager settingsManager, IPrerequisiteHelper prerequisiteHelper)
    : IUvCacheService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <inheritdoc />
    public DirectoryPath GetCacheDirectory()
    {
        // Settings.EnvironmentVariables always carries UV_CACHE_DIR (default or user override)
        return new DirectoryPath(
            settingsManager.Settings.EnvironmentVariables.GetValueOrDefault(
                "UV_CACHE_DIR",
                Path.Combine(settingsManager.LibraryDir, "Assets", "uv", "cache")
            )
        );
    }

    /// <inheritdoc />
    public async Task<long> GetSizeBytesAsync(CancellationToken cancellationToken = default)
    {
        var cacheDir = GetCacheDirectory();
        if (!cacheDir.Exists)
        {
            return 0;
        }

        return await Task.Run(() => cacheDir.GetSize(includeSymbolicLinks: false), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> PurgeAsync(
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!prerequisiteHelper.IsUvInstalled)
        {
            Logger.Info("uv is not installed, skipping cache purge");
            return 0;
        }

        var sizeBefore = await GetSizeBytesAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(new ProgressReport(0, message: "Clearing uv cache...", isIndeterminate: true));

        var processTask = ProcessRunner.GetProcessResultAsync(
            prerequisiteHelper.UvExePath,
            ["cache", "clean"],
            Compat.AppCurrentDir,
            settingsManager.Settings.EnvironmentVariables
        );

        // Report progress by watching the cache shrink. The poll interval scales with the
        // scan cost so huge caches aren't re-enumerated back to back, and scan errors are
        // expected (enumeration races the deletion) — they just skip an update.
        var pollDelay = TimeSpan.FromSeconds(3);

        while (true)
        {
            var completed = await Task.WhenAny(processTask, Task.Delay(pollDelay, cancellationToken))
                .ConfigureAwait(false);

            if (completed == processTask)
                break;

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var currentSize = await GetSizeBytesAsync(cancellationToken).ConfigureAwait(false);
                pollDelay = TimeSpan.FromSeconds(Math.Max(3, stopwatch.Elapsed.TotalSeconds * 2));

                if (sizeBefore > 0)
                {
                    var freedSoFar = Math.Clamp(sizeBefore - currentSize, 0, sizeBefore);
                    progress?.Report(
                        new ProgressReport(
                            current: (ulong)freedSoFar,
                            total: (ulong)sizeBefore,
                            message: $"Cleared {Size.FormatBase10Bytes(freedSoFar)} of {Size.FormatBase10Bytes(sizeBefore)}"
                        )
                    );
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Logger.Debug(e, "uv cache size poll failed, skipping progress update");
            }
        }

        var result = await processTask.ConfigureAwait(false);
        result.EnsureSuccessExitCode();

        var sizeAfter = await GetSizeBytesAsync(cancellationToken).ConfigureAwait(false);
        var freed = Math.Max(0, sizeBefore - sizeAfter);

        progress?.Report(
            new ProgressReport(current: 1, total: 1, message: $"Freed {Size.FormatBase10Bytes(freed)}")
        );

        return freed;
    }
}
