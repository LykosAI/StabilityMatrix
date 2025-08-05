using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Python;

[RegisterSingleton<IUvManager, UvManager>]
public partial class UvManager : IUvManager
{
    private readonly ISettingsManager settingsManager;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private string? uvExecutablePath;
    private DirectoryPath? uvPythonInstallPath;

    // Regex to parse lines from 'uv python list'
    // Example lines:
    //   cpython@3.10.13 (installed at /home/user/.local/share/uv/python/cpython-3.10.13-x86_64-unknown-linux-gnu)
    //   cpython@3.11.7
    //   pypy@3.9.18
    // More complex if it includes source/arch/os:
    //   cpython@3.12.2           x86_64-unknown-linux-gnu (installed at /path)
    // We need a flexible regex. Let's assume a structure like:
    // <key/name_with_version> [optional_arch] [optional_os] [(installed at <path>)]
    // Or simpler from newer uv versions:
    // 3.10.13           cpython  x86_64-unknown-linux-gnu (installed at /path)
    // 3.11.7            cpython  x86_64-unknown-linux-gnu
    private static readonly Regex UvPythonListRegex = UvListRegex();

    public UvManager(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        if (!settingsManager.IsLibraryDirSet)
            return;

        uvPythonInstallPath = new DirectoryPath(settingsManager.LibraryDir, "Assets", "Python");
        uvExecutablePath = Path.Combine(
            settingsManager.LibraryDir,
            "Assets",
            "uv",
            Compat.IsWindows ? "uv.exe" : "uv"
        );
        Logger.Debug($"UvManager initialized with uv executable path: {uvExecutablePath}");
    }

    public async Task<bool> IsUvAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ProcessRunner
                .GetAnsiProcessResultAsync(
                    uvExecutablePath,
                    ["--version"],
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
            return result.IsSuccessExitCode;
        }
        catch (Exception ex)
        {
            Logger.Warn(
                ex,
                $"UV availability check failed for path '{uvExecutablePath}'. UV might not be installed or accessible."
            );
            return false;
        }
    }

    /// <summary>
    /// Lists Python distributions known to UV.
    /// </summary>
    /// <param name="installedOnly">If true, only lists Pythons UV reports as installed.</param>
    /// <param name="onConsoleOutput">Optional callback for console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of UvPythonInfo objects.</returns>
    public async Task<IReadOnlyList<UvPythonInfo>> ListAvailablePythonsAsync(
        bool installedOnly = false,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        uvPythonInstallPath ??= new DirectoryPath(settingsManager.LibraryDir, "Assets", "Python");
        uvExecutablePath ??= Path.Combine(
            settingsManager.LibraryDir,
            "Assets",
            "uv",
            Compat.IsWindows ? "uv.exe" : "uv"
        );

        var args = new ProcessArgsBuilder("python", "list", "--output-format", "json");
        if (settingsManager.Settings.ShowAllAvailablePythonVersions)
        {
            args = args.AddArg("--all-versions");
        }

        var envVars = new Dictionary<string, string>
        {
            // Always use the centrally configured path
            ["UV_PYTHON_INSTALL_DIR"] = uvPythonInstallPath,
        };

        var uvDirectory = Path.GetDirectoryName(uvExecutablePath);

        var result = await ProcessRunner
            .GetProcessResultAsync(uvExecutablePath, args, uvDirectory, envVars)
            .ConfigureAwait(false);

        if (!result.IsSuccessExitCode)
        {
            Logger.Error(
                $"Failed to list UV Python versions. Exit Code: {result.ExitCode}. Error: {result.StandardError}"
            );
            return [];
        }

        var pythons = new List<UvPythonInfo>();
        var json = result.StandardOutput;
        if (string.IsNullOrWhiteSpace(json))
        {
            Logger.Warn("UV Python list output is empty or null.");
            return pythons.AsReadOnly();
        }

        var uvPythonListEntries = JsonSerializer.Deserialize<List<UvPythonListEntry>>(json, JsonSettings);
        if (uvPythonListEntries == null)
        {
            Logger.Warn("Failed to deserialize UV Python list output.");
            return pythons.AsReadOnly();
        }

        var filteredPythons = uvPythonListEntries
            .Where(e => e.Path == null || e.Path.StartsWith(uvPythonInstallPath))
            .Where(e =>
                settingsManager.Settings.ShowAllAvailablePythonVersions
                || (!e.Version.Contains("a") && !e.Version.Contains("b"))
            )
            .Select(e => new UvPythonInfo
            {
                InstallPath = Path.GetDirectoryName(e.Path) ?? string.Empty,
                Version = e.VersionParts,
                Architecture = e.Arch,
                IsInstalled = e.Path != null,
                Key = e.Key,
                Os = e.Os.ToLowerInvariant(),
                Source = e.Implementation.ToLowerInvariant(),
                Libc = e.Libc,
                Variant = e.Variant,
            });

        pythons.AddRange(filteredPythons);

        return pythons.AsReadOnly();
    }

    /// <summary>
    /// Gets information about a specific installed Python version managed by UV.
    /// </summary>
    public async Task<UvPythonInfo?> GetInstalledPythonAsync(
        PyVersion version,
        CancellationToken cancellationToken = default
    )
    {
        var installedPythons = await ListAvailablePythonsAsync(
                installedOnly: true,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        // Find best match (exact or major.minor with highest patch)
        var exactMatch = installedPythons.FirstOrDefault(p => p.IsInstalled && p.Version == version);
        if (exactMatch is { IsInstalled: true })
            return exactMatch; // Struct default is not null

        return installedPythons
            .Where(p => p.IsInstalled && p.Version.Major == version.Major && p.Version.Minor == version.Minor)
            .OrderByDescending(p => p.Version.Micro)
            .FirstOrDefault();
    }

    /// <summary>
    /// Installs a specific Python version using UV.
    /// </summary>
    /// <param name="version">Python version to install (e.g., "3.10" or "3.10.13").</param>
    /// <param name="onConsoleOutput">Optional callback for console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UvPythonInfo for the installed Python, or null if installation failed or info couldn't be retrieved.</returns>
    public async Task<UvPythonInfo?> InstallPythonVersionAsync(
        PyVersion version,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var versionString = $"{version.Major}.{version.Minor}.{version.Micro}";
        if (version.Micro == 0)
        {
            versionString = $"{version.Major}.{version.Minor}";
        }

        var args = new ProcessArgsBuilder("python", "install", versionString);
        var envVars = new Dictionary<string, string>
        {
            // Always use the centrally configured path
            ["UV_PYTHON_INSTALL_DIR"] = uvPythonInstallPath,
        };

        Logger.Debug(
            $"Setting UV_PYTHON_INSTALL_DIR to central path '{uvPythonInstallPath}' for Python {versionString} installation."
        );
        Directory.CreateDirectory(uvPythonInstallPath);

        var processResult = await ProcessRunner
            .GetAnsiProcessResultAsync(
                uvExecutablePath,
                args,
                environmentVariables: envVars,
                outputDataReceived: onConsoleOutput,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        if (!processResult.IsSuccessExitCode)
        { /* Log error */
            return null;
        }

        Logger.Info($"UV install command completed for Python {versionString}. Verifying...");

        // Verification Strategy 1: Use GetInstalledPythonAsync
        var installedPythonInfo = await GetInstalledPythonAsync(version, cancellationToken)
            .ConfigureAwait(false);
        if (
            installedPythonInfo is { IsInstalled: true }
            && !string.IsNullOrWhiteSpace(installedPythonInfo.Value.InstallPath)
        )
        {
            var verifiedInstall = new PyInstallation(
                installedPythonInfo.Value.Version,
                installedPythonInfo.Value.InstallPath
            );
            if (verifiedInstall.Exists())
            {
                Logger.Info(
                    $"Verified install via GetInstalledPythonAsync: {installedPythonInfo.Value.Version} at {installedPythonInfo.Value.InstallPath}"
                );
                return installedPythonInfo.Value;
            }
            Logger.Warn(
                $"GetInstalledPythonAsync found path {installedPythonInfo.Value.InstallPath} but PyInstallation.Exists() failed."
            );
        }
        else
        {
            Logger.Warn(
                $"Could not find Python {version} via GetInstalledPythonAsync after install command."
            );
        }

        // Verification Strategy 2 (Fallback): Look inside the known parent directory
        Logger.Debug($"Attempting fallback path discovery in central directory: {uvPythonInstallPath}");
        try
        {
            var subdirectories = Directory.GetDirectories(uvPythonInstallPath);
            var potentialDirs = subdirectories
                .Select(dir => new { Path = dir, DirInfo = new DirectoryInfo(dir) })
                .Where(x =>
                    x.DirInfo.Name.StartsWith("cpython-", StringComparison.OrdinalIgnoreCase)
                    || x.DirInfo.Name.StartsWith("pypy-", StringComparison.OrdinalIgnoreCase)
                )
                .Where(x => x.DirInfo.Name.Contains($"{version.Major}.{version.Minor}"))
                .OrderByDescending(x => x.DirInfo.CreationTimeUtc)
                .ToList();

            foreach (var potentialDir in potentialDirs)
            {
                var actualInstallPath = potentialDir.Path;
                var pyInstallCheck = new PyInstallation(version, actualInstallPath);
                if (!pyInstallCheck.Exists())
                    continue;

                Logger.Info($"Fallback discovery found likely installation at: {actualInstallPath}");
                var inferredKey = Path.GetFileName(actualInstallPath);
                var inferredSource = inferredKey.Split('-')[0];
                return new UvPythonInfo(
                    version,
                    actualInstallPath,
                    true,
                    inferredSource,
                    null,
                    null,
                    inferredKey,
                    null,
                    null
                );
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error during fallback path discovery in {uvPythonInstallPath}");
        }

        Logger.Error($"Failed to verify and locate Python {version} after UV install command.");
        return null;
    }

    [GeneratedRegex(
        @"^\s*(?<key>[a-zA-Z0-9_.-]+(?:[\+\-][a-zA-Z0-9_.-]+)?)\s+(?<status_or_path>.+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        "en-US"
    )]
    private static partial Regex UvListRegex();
}
