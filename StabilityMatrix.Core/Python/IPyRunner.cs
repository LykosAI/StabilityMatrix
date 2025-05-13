using Python.Runtime;
using StabilityMatrix.Core.Python.Interop;

namespace StabilityMatrix.Core.Python;

public interface IPyRunner
{
    PyIOStream? StdOutStream { get; }
    PyIOStream? StdErrStream { get; }

    /// <summary>
    /// Initializes the Python runtime using the embedded dll.
    /// </summary>
    Task Initialize();

    /// <summary>
    /// Switch to a specific Python installation
    /// </summary>
    Task SwitchToInstallation(PyVersion version);

    /// <summary>
    /// One-time setup for get-pip
    /// </summary>
    Task SetupPip(PyVersion? version = null);

    /// <summary>
    /// Install a Python package with pip
    /// </summary>
    Task InstallPackage(string package, PyVersion? version = null);

    /// <summary>
    /// Run a Function with PyRunning lock as a Task with GIL.
    /// </summary>
    Task<T> RunInThreadWithLock<T>(
        Func<T> func,
        TimeSpan? waitTimeout = null,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Run an Action with PyRunning lock as a Task with GIL.
    /// </summary>
    Task RunInThreadWithLock(
        Action action,
        TimeSpan? waitTimeout = null,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Evaluate Python expression and return its value as a string
    /// </summary>
    Task<string> Eval(string expression);

    /// <summary>
    /// Evaluate Python expression and return its value
    /// </summary>
    Task<T> Eval<T>(string expression);

    /// <summary>
    /// Execute Python code without returning a value
    /// </summary>
    Task Exec(string code);

    /// <summary>
    /// Return the Python version as a PyVersionInfo struct
    /// </summary>
    Task<PyVersionInfo> GetVersionInfo();

    /// <summary>
    /// Get Python directory name for the given version
    /// </summary>
    string GetPythonDirName(PyVersion? version = null);

    /// <summary>
    /// Get Python directory for the given version
    /// </summary>
    string GetPythonDir(PyVersion? version = null);

    /// <summary>
    /// Get Python DLL path for the given version
    /// </summary>
    string GetPythonDllPath(PyVersion? version = null);

    /// <summary>
    /// Get Python executable path for the given version
    /// </summary>
    string GetPythonExePath(PyVersion? version = null);

    /// <summary>
    /// Get Pip executable path for the given version
    /// </summary>
    string GetPipExePath(PyVersion? version = null);
}
