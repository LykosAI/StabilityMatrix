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
    public static readonly PyVersion Python_3_10_17 = new(3, 10, 17);

    /// <summary>
    /// List of available Python versions
    /// </summary>
    public static readonly IReadOnlyList<PyVersion> AvailableVersions = new List<PyVersion>
    {
        Python_3_10_11,
        Python_3_10_17
    };

    /// <summary>
    /// The default Python version to use if none is specified
    /// </summary>
    public static readonly PyVersion DefaultVersion = Python_3_10_17;

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
}
