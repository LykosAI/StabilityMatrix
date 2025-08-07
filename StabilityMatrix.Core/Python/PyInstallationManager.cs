using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Manages multiple Python installations, potentially leveraging UV.
/// </summary>
[RegisterSingleton<IPyInstallationManager, PyInstallationManager>]
public class PyInstallationManager(IUvManager uvManager, ISettingsManager settingsManager)
    : IPyInstallationManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Default Python versions - these are TARGET versions SM knows about
    public static readonly PyVersion Python_3_10_11 = new(3, 10, 11);
    public static readonly PyVersion Python_3_10_17 = new(3, 10, 17);
    public static readonly PyVersion Python_3_11_9 = new(3, 11, 9);
    public static readonly PyVersion Python_3_12_10 = new(3, 12, 10);

    /// <summary>
    /// List of preferred/target Python versions StabilityMatrix officially supports.
    /// UV can be used to fetch these if not present.
    /// </summary>
    public static readonly IReadOnlyList<PyVersion> OldVersions = new List<PyVersion>
    {
        Python_3_10_11,
    }.AsReadOnly();

    /// <summary>
    /// The default Python version to use if none is specified.
    /// </summary>
    public static readonly PyVersion DefaultVersion = Python_3_10_11;

    /// <summary>
    /// Gets all discoverable Python installations (legacy and UV-managed).
    /// This is now an async method.
    /// </summary>
    public async Task<IEnumerable<PyInstallation>> GetAllInstallationsAsync()
    {
        var allInstallations = new List<PyInstallation>();
        var discoveredInstallPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // To avoid duplicates by path

        // 1. Legacy/Bundled Installations (based on TargetVersions and expected paths)
        Logger.Debug("Discovering legacy/bundled Python installations...");
        foreach (var version in OldVersions)
        {
            // The PyInstallation constructor (PyVersion version) now calculates the default path.
            var legacyPyInstall = new PyInstallation(version);
            if (legacyPyInstall.Exists() && discoveredInstallPaths.Add(legacyPyInstall.InstallPath))
            {
                allInstallations.Add(legacyPyInstall);
                Logger.Debug($"Found legacy Python: {legacyPyInstall}");
            }
        }

        // 2. UV-Managed Installations
        if (await uvManager.IsUvAvailableAsync().ConfigureAwait(false))
        {
            Logger.Debug("Discovering UV-managed Python installations...");
            try
            {
                var uvPythons = await uvManager
                    .ListAvailablePythonsAsync(installedOnly: true)
                    .ConfigureAwait(false);
                foreach (var uvPythonInfo in uvPythons)
                {
                    if (string.IsNullOrWhiteSpace(uvPythonInfo.InstallPath))
                        continue;

                    if (discoveredInstallPaths.Add(uvPythonInfo.InstallPath)) // Check if we haven't already added this path (e.g., UV installed to a legacy spot)
                    {
                        var uvPyInstall = new PyInstallation(uvPythonInfo.Version, uvPythonInfo.InstallPath);
                        if (uvPyInstall.Exists()) // Double check, UV said it's installed
                        {
                            allInstallations.Add(uvPyInstall);
                            Logger.Debug($"Found UV-managed Python: {uvPyInstall}");
                        }
                        else
                        {
                            Logger.Warn(
                                $"UV listed Python at {uvPythonInfo.InstallPath} as installed, but PyInstallation.Exists() check failed."
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to list UV-managed Python installations.");
            }
        }
        else
        {
            Logger.Debug("UV management of base Pythons is enabled, but UV is not available/detected.");
        }

        // Return distinct by version, prioritizing (if necessary, though path check helps)
        // For now, just distinct by the PyInstallation object itself (which considers version and path)
        return allInstallations.Distinct().OrderBy(p => p.Version).ToList();
    }

    public async Task<IReadOnlyList<UvPythonInfo>> GetAllAvailablePythonsAsync()
    {
        var allPythons = await uvManager.ListAvailablePythonsAsync().ConfigureAwait(false);
        Func<UvPythonInfo, bool> isSupportedVersion = settingsManager.Settings.ShowAllAvailablePythonVersions
            ? p => p is { Source: "cpython", Version.Minor: >= 10 }
            : p => p is { Source: "cpython", Version.Minor: >= 10 and <= 12 };

        var filteredPythons = allPythons.Where(isSupportedVersion).OrderBy(p => p.Version).ToList();
        var legacyPythonPath = Path.Combine(settingsManager.LibraryDir, "Assets", "Python310");

        if (
            filteredPythons.Any(x => x.Version == Python_3_10_11 && x.InstallPath == legacyPythonPath)
            is false
        )
        {
            var legacyPythonKey =
                Compat.IsWindows ? "python-3.10.11-embed-amd64"
                : Compat.IsMacOS ? "cpython-3.10.11-macos-arm64"
                : "cpython-3.10.11-x86_64-unknown-linux-gnu";

            filteredPythons.Insert(
                0,
                new UvPythonInfo(
                    Python_3_10_11,
                    legacyPythonPath,
                    true,
                    "cpython",
                    null,
                    null,
                    legacyPythonKey,
                    null,
                    null
                )
            );
        }

        return filteredPythons;
    }

    /// <summary>
    /// Gets an installation for a specific version.
    /// If not found, and UV is configured, it may attempt to install it using UV.
    /// This is now an async method.
    /// </summary>
    public async Task<PyInstallation> GetInstallationAsync(PyVersion version)
    {
        // 1. Try to find an already existing installation (legacy or UV-managed)
        var existingInstallations = await GetAllInstallationsAsync().ConfigureAwait(false);

        // Try exact match first
        var exactMatch = existingInstallations.FirstOrDefault(p => p.Version == version);
        if (exactMatch != null)
        {
            Logger.Debug($"Found existing exact match for Python {version}: {exactMatch.InstallPath}");
            return exactMatch;
        }

        // 2. If not found, and UV is allowed to install missing base Pythons, try to install it with UV
        if (await uvManager.IsUvAvailableAsync().ConfigureAwait(false))
        {
            Logger.Info($"Python {version} not found. Attempting to install with UV.");
            try
            {
                var installedUvPython = await uvManager
                    .InstallPythonVersionAsync(version)
                    .ConfigureAwait(false);
                if (
                    installedUvPython.HasValue
                    && !string.IsNullOrWhiteSpace(installedUvPython.Value.InstallPath)
                )
                {
                    var newPyInstall = new PyInstallation(
                        installedUvPython.Value.Version,
                        installedUvPython.Value.InstallPath
                    );
                    if (newPyInstall.Exists())
                    {
                        Logger.Info(
                            $"Successfully installed Python {installedUvPython.Value.Version} with UV at {newPyInstall.InstallPath}"
                        );
                        return newPyInstall;
                    }

                    Logger.Error(
                        $"UV reported successful install of Python {installedUvPython.Value.Version} at {newPyInstall.InstallPath}, but PyInstallation.Exists() check failed."
                    );
                }
                else
                {
                    Logger.Warn(
                        $"UV failed to install Python {version}. Result from UV manager was null or had no path."
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error attempting to install Python {version} with UV.");
            }
        }

        // 3. Fallback: Return a PyInstallation object representing the *expected* legacy path.
        //    The caller can then check .Exists() on it.
        //    This maintains compatibility with code that might expect a PyInstallation object even if the files aren't there.
        Logger.Warn(
            $"Python {version} not found and UV installation was not attempted or failed. Returning prospective legacy PyInstallation object."
        );
        return new PyInstallation(version); // This constructor uses the default/legacy path.
    }

    public async Task<PyInstallation> GetDefaultInstallationAsync()
    {
        return await GetInstallationAsync(DefaultVersion).ConfigureAwait(false);
    }
}
