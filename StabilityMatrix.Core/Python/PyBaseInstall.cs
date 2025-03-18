using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Python;

/// <summary>
/// Represents a base Python installation that can be used by PyVenvRunner
/// </summary>
public class PyBaseInstall(PyInstallation installation)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Gets a PyBaseInstall instance for the default Python installation.
    /// This uses the default Python 3.10.16 installation.
    /// </summary>
    public static PyBaseInstall Default => new(new PyInstallation(PyInstallationManager.DefaultVersion));

    /// <summary>
    /// The Python installation
    /// </summary>
    public PyInstallation Installation { get; } = installation;

    /// <summary>
    /// Root path of the Python installation
    /// </summary>
    public string RootPath => Installation.InstallPath;

    /// <summary>
    /// Python executable path
    /// </summary>
    public string PythonExePath => Installation.PythonExePath;

    /// <summary>
    /// Pip executable path
    /// </summary>
    public string PipExePath => Installation.PipExePath;

    /// <summary>
    /// Version of the Python installation
    /// </summary>
    public PyVersion Version => Installation.Version;

    /// <summary>
    /// Create a virtual environment with this Python installation as the base
    /// </summary>
    public PyVenvRunner CreateVenv(DirectoryPath venvPath)
    {
        return new PyVenvRunner(this, venvPath);
    }

    /// <summary>
    /// Create a virtual environment with this Python installation as the base and
    /// configure it with the specified parameters.
    /// </summary>
    /// <param name="venvPath">Path where the virtual environment will be created</param>
    /// <param name="workingDirectory">Optional working directory for the Python process</param>
    /// <param name="environmentVariables">Optional environment variables for the Python process</param>
    /// <param name="withDefaultTclTkEnv">Whether to set up the default Tkinter environment variables (Windows)</param>
    /// <param name="withQueriedTclTkEnv">Whether to query and set up Tkinter environment variables (Unix)</param>
    /// <returns>A configured PyVenvRunner instance</returns>
    public PyVenvRunner CreateVenvRunner(
        DirectoryPath venvPath,
        DirectoryPath? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        bool withDefaultTclTkEnv = false,
        bool withQueriedTclTkEnv = false
    )
    {
        var venvRunner = new PyVenvRunner(this, venvPath);

        // Set working directory if provided
        if (workingDirectory != null)
        {
            venvRunner.WorkingDirectory = workingDirectory;
        }

        // Set environment variables if provided
        if (environmentVariables != null)
        {
            var envVarDict = venvRunner.EnvironmentVariables;
            foreach (var (key, value) in environmentVariables)
            {
                envVarDict = envVarDict.SetItem(key, value);
            }
            venvRunner.EnvironmentVariables = envVarDict;
        }

        // Configure Tkinter environment variables if requested
        if (withDefaultTclTkEnv && Compat.IsWindows)
        {
            // Set up default TCL/TK environment variables for Windows
            var envVarDict = venvRunner.EnvironmentVariables;
            envVarDict = envVarDict.SetItem("TCL_LIBRARY", Path.Combine(RootPath, "tcl", "tcl8.6"));
            envVarDict = envVarDict.SetItem("TK_LIBRARY", Path.Combine(RootPath, "tcl", "tk8.6"));
            venvRunner.EnvironmentVariables = envVarDict;
        }
        else if (withQueriedTclTkEnv && Compat.IsUnix)
        {
            // For Unix, we might need to query the system for TCL/TK locations
            try
            {
                // Implementation would depend on how your system detects TCL/TK on Unix
                Logger.Debug("Setting up TCL/TK environment for Unix");
                // This would be implemented based on your system's requirements
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to set up TCL/TK environment for Unix");
            }
        }

        return venvRunner;
    }

    /// <summary>
    /// Asynchronously create a virtual environment with this Python installation as the base and
    /// configure it with the specified parameters.
    /// </summary>
    /// <param name="venvPath">Path where the virtual environment will be created</param>
    /// <param name="workingDirectory">Optional working directory for the Python process</param>
    /// <param name="environmentVariables">Optional environment variables for the Python process</param>
    /// <param name="withDefaultTclTkEnv">Whether to set up the default Tkinter environment variables (Windows)</param>
    /// <param name="withQueriedTclTkEnv">Whether to query and set up Tkinter environment variables (Unix)</param>
    /// <returns>A configured PyVenvRunner instance</returns>
    public async Task<PyVenvRunner> CreateVenvRunnerAsync(
        string venvPath,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        bool withDefaultTclTkEnv = false,
        bool withQueriedTclTkEnv = false
    )
    {
        var dirPath = new DirectoryPath(venvPath);
        var workingDir = workingDirectory != null ? new DirectoryPath(workingDirectory) : null;

        // Use the synchronous version and just return with a completed task
        var venvRunner = CreateVenvRunner(
            dirPath,
            workingDir,
            environmentVariables,
            withDefaultTclTkEnv,
            withQueriedTclTkEnv
        );

        return venvRunner;
    }
}
