using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Manages multiple Python installations
/// </summary>
[RegisterSingleton<IPyInstallationManager, PyInstallationManager>]
public class PyInstallationManager() : IPyInstallationManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Default Python versions
    public static readonly PyVersion Python_3_10_11 = new(3, 10, 11);
    public static readonly PyVersion Python_3_10_16 = new(3, 10, 16);

    /// <summary>
    /// List of available Python versions
    /// </summary>
    public static readonly IReadOnlyList<PyVersion> AvailableVersions = new List<PyVersion>
    {
        Python_3_10_11,
        Python_3_10_16
    };

    /// <summary>
    /// The default Python version to use if none is specified
    /// </summary>
    public static readonly PyVersion DefaultVersion = Python_3_10_11;

    /// <summary>
    /// Gets all available Python installations
    /// </summary>
    public IEnumerable<PyInstallation> GetAllInstallations()
    {
        foreach (var version in AvailableVersions)
        {
            yield return new PyInstallation(version);
        }
    }

    /// <summary>
    /// Gets all installed Python installations
    /// </summary>
    public IEnumerable<PyInstallation> GetInstalledInstallations()
    {
        foreach (var installation in GetAllInstallations())
        {
            if (installation.Exists())
            {
                yield return installation;
            }
        }
    }

    /// <summary>
    /// Gets an installation for a specific version
    /// </summary>
    public PyInstallation GetInstallation(PyVersion version)
    {
        return new PyInstallation(version);
    }

    /// <summary>
    /// Gets the default installation
    /// </summary>
    public PyInstallation GetDefaultInstallation()
    {
        return GetInstallation(DefaultVersion);
    }

    /// <summary>
    /// Checks if legacy directory structure exists and migrates it to the new format
    /// </summary>
    public async Task MigrateFromLegacyDirectories()
    {
        var legacyDir = Path.Combine(GlobalConfig.LibraryDir, "Assets", "Python310");
        if (Directory.Exists(legacyDir))
        {
            Logger.Info("Found legacy Python310 directory, attempting to migrate");

            // Construct the path for the new directory with micro version
            var newDir = Path.Combine(GlobalConfig.LibraryDir, "Assets", "Python31011");

            // Skip if the new directory already exists (already migrated or both installed separately)
            if (Directory.Exists(newDir))
            {
                Logger.Info("New Python31011 directory already exists, skipping migration");
                return;
            }

            try
            {
                // Create parent directory if it doesn't exist
                var parentDir = Path.GetDirectoryName(newDir);
                if (parentDir != null && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                // Move the directory
                await Task.Run(() => Directory.Move(legacyDir, newDir)).ConfigureAwait(false);
                Logger.Info("Successfully migrated legacy Python310 directory to Python31011");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to migrate legacy Python310 directory to Python31011");
                throw;
            }
        }
    }
}
