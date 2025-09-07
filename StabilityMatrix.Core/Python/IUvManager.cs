using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Python;

public interface IUvManager
{
    Task<bool> IsUvAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Python distributions known to UV.
    /// </summary>
    /// <param name="installedOnly">If true, only lists Pythons UV reports as installed.</param>
    /// <param name="onConsoleOutput">Optional callback for console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of UvPythonInfo objects.</returns>
    Task<IReadOnlyList<UvPythonInfo>> ListAvailablePythonsAsync(
        bool installedOnly = false,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets information about a specific installed Python version managed by UV.
    /// </summary>
    Task<UvPythonInfo?> GetInstalledPythonAsync(
        PyVersion version,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Installs a specific Python version using UV.
    /// </summary>
    /// <param name="version">Python version to install (e.g., "3.10" or "3.10.13").</param>
    /// <param name="targetInstallDirectory">Optional. If provided, UV_PYTHON_INSTALL_DIR will be set for the uv process.</param>
    /// <param name="onConsoleOutput">Optional callback for console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UvPythonInfo for the installed Python, or null if installation failed or info couldn't be retrieved.</returns>
    Task<UvPythonInfo?> InstallPythonVersionAsync(
        PyVersion version,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );
}
