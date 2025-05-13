namespace StabilityMatrix.Core.Python;

/// <summary>
/// Interface for managing Python installations
/// </summary>
public interface IPyInstallationManager
{
    /// <summary>
    /// Gets all discoverable Python installations (legacy and UV-managed).
    /// This is now an async method.
    /// </summary>
    Task<IEnumerable<PyInstallation>> GetAllInstallationsAsync();

    /// <summary>
    /// Gets an installation for a specific version.
    /// If not found, and UV is configured, it may attempt to install it using UV.
    /// This is now an async method.
    /// </summary>
    Task<PyInstallation> GetInstallationAsync(PyVersion version);

    /// <summary>
    /// Gets the default installation.
    /// This is now an async method.
    /// </summary>
    Task<PyInstallation> GetDefaultInstallationAsync();

    Task<IReadOnlyList<UvPythonInfo>> GetAllAvailablePythonsAsync();
}
