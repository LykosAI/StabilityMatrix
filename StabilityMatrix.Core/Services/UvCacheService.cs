using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
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
    /// unlike deleting the directory). Returns the number of bytes freed.
    /// </summary>
    Task<long> PurgeAsync(CancellationToken cancellationToken = default);
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
    public async Task<long> PurgeAsync(CancellationToken cancellationToken = default)
    {
        if (!prerequisiteHelper.IsUvInstalled)
        {
            Logger.Info("uv is not installed, skipping cache purge");
            return 0;
        }

        var sizeBefore = await GetSizeBytesAsync(cancellationToken).ConfigureAwait(false);

        var result = await ProcessRunner
            .GetProcessResultAsync(
                prerequisiteHelper.UvExePath,
                ["cache", "clean"],
                Compat.AppCurrentDir,
                settingsManager.Settings.EnvironmentVariables
            )
            .ConfigureAwait(false);

        if (!result.IsSuccessExitCode)
        {
            Logger.Error(
                "uv cache clean failed. Exit Code: {ExitCode}. Error: {Error}",
                result.ExitCode,
                result.StandardError
            );
            return 0;
        }

        var sizeAfter = await GetSizeBytesAsync(cancellationToken).ConfigureAwait(false);
        return Math.Max(0, sizeBefore - sizeAfter);
    }
}
