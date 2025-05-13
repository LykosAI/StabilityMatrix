using System.Diagnostics.CodeAnalysis;
using Injectio.Attributes;
using NLog;
using Python.Runtime;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python.Interop;

namespace StabilityMatrix.Core.Python;

[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record struct PyVersionInfo(int Major, int Minor, int Micro, string ReleaseLevel, int Serial);

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[RegisterSingleton<IPyRunner, PyRunner>]
public class PyRunner : IPyRunner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Set by ISettingsManager.TryFindLibrary()
    public static DirectoryPath HomeDir { get; set; } = string.Empty;

    // The installation manager for handling different Python versions
    private readonly IPyInstallationManager installationManager;

    // The current Python installation being used
    private PyInstallation? currentInstallation;

    /// <summary>
    /// Get the Python directory name for the given version, or the default version if none specified
    /// </summary>
    public string GetPythonDirName(PyVersion? version = null) =>
        version != null
            ? $"Python{version.Value.Major}{version.Value.Minor}{version.Value.Micro}"
            : "Python310"; // Default to 3.10.11 for compatibility

    /// <summary>
    /// Get the Python directory for the given version, or the default version if none specified
    /// </summary>
    public string GetPythonDir(PyVersion? version = null) =>
        Path.Combine(GlobalConfig.LibraryDir, "Assets", GetPythonDirName(version));

    /// <summary>
    /// Get the Python DLL path for the given version, or the default version if none specified
    /// </summary>
    public string GetPythonDllPath(PyVersion? version = null)
    {
        var pythonDir = GetPythonDir(version);
        var relativePath =
            version != null
                ? Compat.Switch(
                    (PlatformKind.Windows, $"python{version.Value.Major}{version.Value.Minor}.dll"),
                    (
                        PlatformKind.Linux,
                        Path.Combine("lib", $"libpython{version.Value.Major}.{version.Value.Minor}.so")
                    ),
                    (
                        PlatformKind.MacOS,
                        Path.Combine("lib", $"libpython{version.Value.Major}.{version.Value.Minor}.dylib")
                    )
                )
                : RelativePythonDllPath;

        return Path.Combine(pythonDir, relativePath);
    }

    /// <summary>
    /// Get the Python executable path for the given version, or the default version if none specified
    /// </summary>
    public string GetPythonExePath(PyVersion? version = null)
    {
        var pythonDir = GetPythonDir(version);
        return Compat.Switch(
            (PlatformKind.Windows, Path.Combine(pythonDir, "python.exe")),
            (PlatformKind.Linux, Path.Combine(pythonDir, "bin", "python3")),
            (PlatformKind.MacOS, Path.Combine(pythonDir, "bin", "python3"))
        );
    }

    /// <summary>
    /// Get the pip executable path for the given version, or the default version if none specified
    /// </summary>
    public string GetPipExePath(PyVersion? version = null)
    {
        var pythonDir = GetPythonDir(version);
        return Compat.Switch(
            (PlatformKind.Windows, Path.Combine(pythonDir, "Scripts", "pip.exe")),
            (PlatformKind.Linux, Path.Combine(pythonDir, "bin", "pip3")),
            (PlatformKind.MacOS, Path.Combine(pythonDir, "bin", "pip3"))
        );
    }

    // Legacy properties for compatibility - these use the default Python version
    public const string PythonDirName = "Python310";
    public static string PythonDir => Path.Combine(GlobalConfig.LibraryDir, "Assets", PythonDirName);

    /// <summary>
    /// Path to the Python Linked library relative from the Python directory.
    /// </summary>
    public static string RelativePythonDllPath =>
        Compat.Switch(
            (PlatformKind.Windows, "python310.dll"),
            (PlatformKind.Linux, Path.Combine("lib", "libpython3.10.so")),
            (PlatformKind.MacOS, Path.Combine("lib", "libpython3.10.dylib"))
        );

    public static string PythonDllPath => Path.Combine(PythonDir, RelativePythonDllPath);
    public static string PythonExePath =>
        Compat.Switch(
            (PlatformKind.Windows, Path.Combine(PythonDir, "python.exe")),
            (PlatformKind.Linux, Path.Combine(PythonDir, "bin", "python3")),
            (PlatformKind.MacOS, Path.Combine(PythonDir, "bin", "python3"))
        );
    public static string PipExePath =>
        Compat.Switch(
            (PlatformKind.Windows, Path.Combine(PythonDir, "Scripts", "pip.exe")),
            (PlatformKind.Linux, Path.Combine(PythonDir, "bin", "pip3")),
            (PlatformKind.MacOS, Path.Combine(PythonDir, "bin", "pip3"))
        );

    public static string GetPipPath => Path.Combine(PythonDir, "get-pip.pyc");

    public static string VenvPath => Path.Combine(PythonDir, "Scripts", "virtualenv" + Compat.ExeExtension);

    public static bool PipInstalled => File.Exists(PipExePath);
    public static bool VenvInstalled => File.Exists(VenvPath);

    private static readonly SemaphoreSlim PyRunning = new(1, 1);

    public PyIOStream? StdOutStream { get; private set; }
    public PyIOStream? StdErrStream { get; private set; }

    public PyRunner(IPyInstallationManager installationManager)
    {
        this.installationManager = installationManager;
    }

    /// <summary>
    /// Switch to a specific Python installation
    /// </summary>
    public async Task SwitchToInstallation(PyVersion version)
    {
        // If Python is already initialized with a different version, we need to shutdown first
        if (PythonEngine.IsInitialized && currentInstallation?.Version != version)
        {
            // hacky stuff until Python.NET stops using BinaryFormatter
            AppContext.SetSwitch(
                "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization",
                true
            );
            Logger.Info("Shutting down previous Python runtime for version switch");
            PythonEngine.Shutdown();
            AppContext.SetSwitch(
                "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization",
                false
            );
        }

        // If not initialized or we had to shutdown, initialize with the new version
        if (!PythonEngine.IsInitialized)
        {
            // Get the installation for this version
            var installation = await installationManager.GetInstallationAsync(version).ConfigureAwait(false);
            if (!installation.Exists())
            {
                throw new FileNotFoundException(
                    $"Python {version} installation not found at {installation.InstallPath}"
                );
            }

            currentInstallation = installation;

            // Initialize with this installation
            await InitializeWithInstallation(installation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Initialize Python runtime with a specific installation
    /// </summary>
    private async Task InitializeWithInstallation(PyInstallation installation)
    {
        if (PythonEngine.IsInitialized)
            return;

        Logger.Info("Setting PYTHONHOME={PythonDir}", installation.InstallPath.ToRepr());

        // Append Python path to PATH
        var newEnvPath = Compat.GetEnvPathWithExtensions(installation.InstallPath);
        Logger.Debug("Setting PATH={NewEnvPath}", newEnvPath.ToRepr());
        Environment.SetEnvironmentVariable("PATH", newEnvPath, EnvironmentVariableTarget.Process);

        Logger.Info("Initializing Python runtime with DLL: {DllPath}", installation.PythonDllPath);
        // Check PythonDLL exists
        if (!File.Exists(installation.PythonDllPath))
        {
            throw new FileNotFoundException("Python linked library not found", installation.PythonDllPath);
        }

        Runtime.PythonDLL = installation.PythonDllPath;
        PythonEngine.PythonHome = installation.InstallPath;
        PythonEngine.Initialize();
        PythonEngine.BeginAllowThreads();

        // Redirect stdout and stderr
        StdOutStream = new PyIOStream();
        StdErrStream = new PyIOStream();
        await RunInThreadWithLock(() =>
            {
                var sys =
                    Py.Import("sys") as PyModule ?? throw new NullReferenceException("sys module not found");
                sys.Set("stdout", StdOutStream);
                sys.Set("stderr", StdErrStream);
            })
            .ConfigureAwait(false);
    }

    /// <summary>$
    /// Initializes the Python runtime using the embedded dll.
    /// Can be called with no effect after initialization.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if Python DLL not found.</exception>
    public async Task Initialize()
    {
        if (PythonEngine.IsInitialized)
            return;

        // Get the default installation
        var defaultInstallation = await installationManager
            .GetDefaultInstallationAsync()
            .ConfigureAwait(false);
        if (!defaultInstallation.Exists())
        {
            throw new FileNotFoundException(
                $"Default Python installation not found at {defaultInstallation.InstallPath}"
            );
        }

        currentInstallation = defaultInstallation;
        await InitializeWithInstallation(defaultInstallation).ConfigureAwait(false);
    }

    /// <summary>
    /// One-time setup for get-pip
    /// </summary>
    public async Task SetupPip(PyVersion? version = null)
    {
        // Use either the specified version or the current installation
        var installation =
            version != null
                ? await installationManager.GetInstallationAsync(version.Value).ConfigureAwait(false)
                : currentInstallation
                    ?? await installationManager.GetDefaultInstallationAsync().ConfigureAwait(false);

        var getPipPath = Path.Combine(installation.InstallPath, "get-pip.pyc");

        if (!File.Exists(getPipPath))
        {
            throw new FileNotFoundException("get-pip not found", getPipPath);
        }

        await ProcessRunner
            .GetProcessResultAsync(installation.PythonExePath, ["-m", "get-pip"])
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);

        // Pip version 24.1 deprecated numpy star requirement spec used by some packages
        // So make the base pip less than that for compatibility, venvs can upgrade themselves if needed
        await ProcessRunner
            .GetProcessResultAsync(
                installation.PythonExePath,
                ["-m", "pip", "install", "pip==23.3.2", "setuptools==69.5.1"]
            )
            .EnsureSuccessExitCode()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Install a Python package with pip
    /// </summary>
    public async Task InstallPackage(string package, PyVersion? version = null)
    {
        // Use either the specified version or the current installation
        var installation =
            version != null
                ? await installationManager.GetInstallationAsync(version.Value).ConfigureAwait(false)
                : currentInstallation
                    ?? await installationManager.GetDefaultInstallationAsync().ConfigureAwait(false);

        if (!File.Exists(installation.PipExePath))
        {
            throw new FileNotFoundException("pip not found", installation.PipExePath);
        }
        var result = await ProcessRunner
            .GetProcessResultAsync(installation.PythonExePath, $"-m pip install {package}")
            .ConfigureAwait(false);
        result.EnsureSuccessExitCode();
    }

    /// <summary>
    /// Run a Function with PyRunning lock as a Task with GIL.
    /// </summary>
    /// <param name="func">Function to run.</param>
    /// <param name="waitTimeout">Time limit for waiting on PyRunning lock.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <exception cref="OperationCanceledException">cancelToken was canceled, or waitTimeout expired.</exception>
    public async Task<T> RunInThreadWithLock<T>(
        Func<T> func,
        TimeSpan? waitTimeout = null,
        CancellationToken cancelToken = default
    )
    {
        // Wait to acquire PyRunning lock
        await PyRunning.WaitAsync(cancelToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                    () =>
                    {
                        using (Py.GIL())
                        {
                            return func();
                        }
                    },
                    cancelToken
                )
                .ConfigureAwait(false);
        }
        finally
        {
            PyRunning.Release();
        }
    }

    /// <summary>
    /// Run an Action with PyRunning lock as a Task with GIL.
    /// </summary>
    /// <param name="action">Action to run.</param>
    /// <param name="waitTimeout">Time limit for waiting on PyRunning lock.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <exception cref="OperationCanceledException">cancelToken was canceled, or waitTimeout expired.</exception>
    public async Task RunInThreadWithLock(
        Action action,
        TimeSpan? waitTimeout = null,
        CancellationToken cancelToken = default
    )
    {
        // Wait to acquire PyRunning lock
        await PyRunning.WaitAsync(cancelToken).ConfigureAwait(false);
        try
        {
            await Task.Run(
                    () =>
                    {
                        using (Py.GIL())
                        {
                            action();
                        }
                    },
                    cancelToken
                )
                .ConfigureAwait(false);
        }
        finally
        {
            PyRunning.Release();
        }
    }

    /// <summary>
    /// Evaluate Python expression and return its value as a string
    /// </summary>
    /// <param name="expression"></param>
    public async Task<string> Eval(string expression)
    {
        return await Eval<string>(expression);
    }

    /// <summary>
    /// Evaluate Python expression and return its value
    /// </summary>
    /// <param name="expression"></param>
    public Task<T> Eval<T>(string expression)
    {
        return RunInThreadWithLock(() =>
        {
            using var scope = Py.CreateScope();
            var result = scope.Eval(expression);

            // For string, cast with __str__()
            if (typeof(T) == typeof(string))
            {
                return result.GetAttr("__str__").Invoke().As<T>();
            }
            return result.As<T>();
        });
    }

    /// <summary>
    /// Execute Python code without returning a value
    /// </summary>
    /// <param name="code"></param>
    public Task Exec(string code)
    {
        return RunInThreadWithLock(() =>
        {
            using var scope = Py.CreateScope();
            scope.Exec(code);
        });
    }

    /// <summary>
    /// Return the Python version as a PyVersionInfo struct
    /// </summary>
    public async Task<PyVersionInfo> GetVersionInfo()
    {
        var info = await Eval<PyObject[]>("tuple(__import__('sys').version_info)");
        return new PyVersionInfo(
            info[0].As<int>(),
            info[1].As<int>(),
            info[2].As<int>(),
            info[3].As<string>(),
            info[4].As<int>()
        );
    }
}
