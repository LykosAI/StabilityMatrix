using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Interface for managing Python installations
/// </summary>
public interface IPyInstallationManager
{
    /// <summary>
    /// Gets all available Python installations
    /// </summary>
    IEnumerable<PyInstallation> GetAllInstallations();

    /// <summary>
    /// Gets all installed Python installations
    /// </summary>
    IEnumerable<PyInstallation> GetInstalledInstallations();

    /// <summary>
    /// Gets an installation for a specific version
    /// </summary>
    PyInstallation GetInstallation(PyVersion version);

    /// <summary>
    /// Gets the default installation
    /// </summary>
    PyInstallation GetDefaultInstallation();

    /// <summary>
    /// Checks if legacy directory structure exists and migrates it to the new format
    /// </summary>
    Task MigrateFromLegacyDirectories();
}
