using System.Text.RegularExpressions;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Python;

[RegisterSingleton<IUvManager, UvManager>]
public partial class UvManager : IUvManager
{
    private readonly ISettingsManager settingsManager;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly string uvExecutablePath;
    private readonly DirectoryPath uvPythonInstallPath;

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
        // Keep implementation from previous correct version (using UvPythonListOutputRegex)
        // ... existing implementation ...
        var args = new ProcessArgsBuilder("python", "list");
        if (settingsManager.Settings.ShowAllAvailablePythonVersions)
        {
            args = args.AddArg("--all-versions");
        }

        var envVars = new Dictionary<string, string>
        {
            // Always use the centrally configured path
            ["UV_PYTHON_INSTALL_DIR"] = uvPythonInstallPath,
        };

        var result = await ProcessRunner
            .GetProcessResultAsync(uvExecutablePath, args, environmentVariables: envVars)
            .ConfigureAwait(false);

        if (!result.IsSuccessExitCode)
        {
            Logger.Error(
                $"Failed to list UV Python versions. Exit Code: {result.ExitCode}. Error: {result.StandardError}"
            );
            return [];
        }

        var pythons = new List<UvPythonInfo>();
        var lines = result.StandardOutput?.SplitLines(StringSplitOptions.RemoveEmptyEntries) ?? [];

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (
                string.IsNullOrWhiteSpace(trimmedLine)
                || trimmedLine.StartsWith("uv ", StringComparison.OrdinalIgnoreCase)
                || trimmedLine.Contains(" distributions:", StringComparison.OrdinalIgnoreCase)
            ) // Skip headers
            {
                continue;
            }

            var match = UvPythonListRegex.Match(trimmedLine);

            if (match.Success)
            {
                var key = match.Groups["key"].Value.Trim();
                var statusOrPath = match.Groups["status_or_path"].Value.Trim();

                // Handle symlinks by removing the -> and everything after it
                if (statusOrPath.Contains(" -> "))
                {
                    statusOrPath = statusOrPath.Substring(0, statusOrPath.IndexOf(" -> ")).Trim();
                }

                string? actualInstallPath = null; // This should be the INNER path (e.g., .../cpython-...)
                var isInstalled = false;
                var isDownloadAvailable = false;

                // --- Path Detection Logic ---
                if (statusOrPath.Equals("<download available>", StringComparison.OrdinalIgnoreCase))
                {
                    isInstalled = false;
                    isDownloadAvailable = true;
                }
                // Check if it looks like a path to an executable -> derive inner path
                else if (
                    File.Exists(statusOrPath)
                    && (
                        statusOrPath.EndsWith("python.exe", StringComparison.OrdinalIgnoreCase)
                        || statusOrPath.Contains("/python3.", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    var exeDir = Path.GetDirectoryName(statusOrPath);
                    var dirName = Path.GetFileName(exeDir);
                    if (
                        dirName != null
                        && (
                            dirName.Equals("bin", StringComparison.OrdinalIgnoreCase)
                            || dirName.Equals("Scripts", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        actualInstallPath = Path.GetDirectoryName(exeDir); // Go one level up to Python root
                    }
                    else
                    {
                        actualInstallPath = exeDir;
                        Logger.Warn(
                            $"Python executable found at '{statusOrPath}' but not in expected bin/Scripts subdir. Assuming parent '{actualInstallPath}' is Python root."
                        );
                    }

                    if (actualInstallPath != null)
                    {
                        // Check if installation exists
                        var quickCheck = new PyInstallation(new PyVersion(0, 0, 0), actualInstallPath); // Use temp version
                        isInstalled = quickCheck.Exists();
                    }
                }
                // Check if it's a directory path -> Assume it's the INNER path
                else if (Directory.Exists(statusOrPath))
                {
                    var quickCheck = new PyInstallation(new PyVersion(0, 0, 0), statusOrPath); // Use temp version
                    isInstalled = quickCheck.Exists();
                    if (isInstalled)
                    {
                        actualInstallPath = statusOrPath;
                    }
                    else
                    {
                        Logger.Trace(
                            $"Path '{statusOrPath}' for key '{key}' exists as directory but doesn't pass PyInstallation.Exists(). Marking as not installed."
                        );
                        isInstalled = false;
                    }
                }
                else
                {
                    isInstalled = false;
                }
                // --- End Path Detection ---

                if (installedOnly && !isInstalled)
                    continue;

                // ... (Parse key for version, source, arch, os as before - using PyVersion.TryParseFromComplexString) ...
                string? source = null;
                PyVersion? pyVersion = null;
                string? architecture = null;
                string? osInfo = null;

                var keyParts = key.Split('-');
                if (keyParts.Length > 1)
                {
                    source = keyParts[0];
                    // ... (robust version parsing logic using PyVersion.TryParseFromComplexString) ...
                    // ... (heuristic arch/os parsing logic) ...
                    for (var i = 1; i < keyParts.Length; ++i)
                    {
                        if (!char.IsDigit(keyParts[i][0]))
                            continue;

                        if (PyVersion.TryParseFromComplexString(keyParts[i], out var parsedVer))
                        {
                            pyVersion = parsedVer;
                            // Infer arch/os from remaining parts
                            if (keyParts.Length > i + 1)
                                architecture = keyParts
                                    .Skip(i + 1)
                                    .FirstOrDefault(p =>
                                        p.Contains("x86_64") || p.Contains("amd64") || p.Contains("arm")
                                    );
                            if (keyParts.Length > i + 1)
                                osInfo = string.Join("-", keyParts.Skip(i + 1).Where(p => p != architecture));
                            break;
                        }

                        if (
                            i + 1 < keyParts.Length
                            && PyVersion.TryParseFromComplexString(
                                $"{keyParts[i]}-{keyParts[i + 1]}",
                                out parsedVer
                            )
                        )
                        {
                            pyVersion = parsedVer;
                            if (keyParts.Length > i + 2)
                                architecture = keyParts
                                    .Skip(i + 2)
                                    .FirstOrDefault(p =>
                                        p.Contains("x86_64") || p.Contains("amd64") || p.Contains("arm")
                                    );
                            if (keyParts.Length > i + 2)
                                osInfo = string.Join("-", keyParts.Skip(i + 2).Where(p => p != architecture));
                            break;
                        }
                    }

                    if (
                        !pyVersion.HasValue
                        && PyVersion.TryParseFromComplexString(
                            string.Join("-", keyParts.Skip(1)),
                            out var fallbackParsedVer
                        )
                    )
                    {
                        pyVersion = fallbackParsedVer;
                    }
                    if (pyVersion.HasValue && architecture == null)
                    {
                        architecture = keyParts.FirstOrDefault(p =>
                            p.Contains("x86_64")
                            || p.Contains("amd64")
                            || p.Contains("arm64")
                            || p.Contains("aarch64")
                        );
                    }

                    if (pyVersion.HasValue && osInfo == null)
                    {
                        var osParts = keyParts
                            .Skip(1)
                            .Where(p => !PyVersion.TryParseFromComplexString(p, out _))
                            .Where(p => p != architecture)
                            .ToList();
                        if (osParts.Any())
                            osInfo = string.Join("-", osParts);
                    }
                }

                if (pyVersion.HasValue)
                {
                    actualInstallPath ??= string.Empty;

                    // Only include Pythons that are:
                    // 1. "<download available>" OR
                    // 2. Installed in our uvPythonInstallPath
                    bool shouldInclude =
                        isDownloadAvailable
                        || (
                            isInstalled
                            && !string.IsNullOrEmpty(actualInstallPath)
                            && actualInstallPath.StartsWith(uvPythonInstallPath)
                        );

                    if (shouldInclude)
                    {
                        pythons.Add(
                            new UvPythonInfo(
                                pyVersion.Value,
                                actualInstallPath,
                                isInstalled,
                                source,
                                architecture,
                                osInfo,
                                key
                            )
                        );
                    }
                }
                else
                {
                    Logger.Warn($"Could not parse PyVersion from UV Python key: '{key}'");
                }
            }
            else
            {
                Logger.Trace($"Line did not match UV Python list output regex: '{trimmedLine}'");
            }
        }

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
                    inferredKey
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
