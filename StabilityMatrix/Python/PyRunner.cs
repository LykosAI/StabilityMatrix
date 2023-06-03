using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Python.Runtime;
using StabilityMatrix.Helper;
using StabilityMatrix.Python.Interop;

namespace StabilityMatrix.Python;

public record struct PyVersionInfo(int Major, int Minor, int Micro, string ReleaseLevel, int Serial);

public class PyRunner : IPyRunner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string RelativeHomePath = @"Assets\Python310";
    private const string RelativeDllPath = @"Assets\Python310\python310.dll";
    private const string RelativeExePath = @"Assets\Python310\python.exe";
    private const string RelativePipExePath = @"Assets\Python310\Scripts\pip.exe";
    private const string RelativeGetPipPath = @"Assets\Python310\get-pip.pyc";
    private const string RelativeVenvPath = @"Assets\Python310\Scripts\virtualenv.exe";
    public static string HomePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeHomePath);
    public static string DllPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeDllPath);
    public static string ExePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeExePath);
    public static string PipExePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativePipExePath);
    public static string GetPipPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeGetPipPath);
    public static string VenvPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeVenvPath);

    public static bool PipInstalled => File.Exists(PipExePath);
    public static bool VenvInstalled => File.Exists(VenvPath);

    private static readonly SemaphoreSlim PyRunning = new(1, 1);

    public PyIOStream? StdOutStream;
    public PyIOStream? StdErrStream;

    /// <summary>
    /// Initializes the Python runtime using the embedded dll.
    /// Can be called with no effect after initialization.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if Python DLL not found.</exception>
    public async Task Initialize()
    {
        if (PythonEngine.IsInitialized) return;

        Logger.Info("Setting PYTHONHOME and PATH to {HomePath}", HomePath);
        Environment.SetEnvironmentVariable("PYTHONHOME", HomePath, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("PATH", HomePath, EnvironmentVariableTarget.Process);

        Logger.Info("Initializing Python runtime with DLL: {DllPath}", DllPath);
        // Check PythonDLL exists
        if (!File.Exists(DllPath))
        {
            Logger.Error("Python DLL not found");
            throw new FileNotFoundException("Python DLL not found", DllPath);
        }
        
        Runtime.PythonDLL = DllPath;
        PythonEngine.Initialize();
        PythonEngine.BeginAllowThreads();

        // Redirect stdout and stderr
        StdOutStream = new PyIOStream();
        StdErrStream = new PyIOStream();
        await RunInThreadWithLock(() =>
        {
            dynamic sys = Py.Import("sys");
            sys.stdout = StdOutStream;
            sys.stderr = StdErrStream;
        });
    }

    /// <summary>
    /// One-time setup for get-pip
    /// </summary>
    public async Task SetupPip()
    {
        if (!File.Exists(GetPipPath))
        {
            throw new FileNotFoundException("get-pip not found", GetPipPath);
        }
        var p = ProcessRunner.StartProcess(ExePath, "-m get-pip");
        await ProcessRunner.WaitForExitConditionAsync(p);
    }

    /// <summary>
    /// Install a Python package with pip
    /// </summary>
    public async Task InstallPackage(string package)
    {
        if (!File.Exists(PipExePath))
        {
            throw new FileNotFoundException("pip not found", PipExePath);
        }
        var p = ProcessRunner.StartProcess(PipExePath, $"install {package}");
        await ProcessRunner.WaitForExitConditionAsync(p);
    }

    /// <summary>
    /// Run a Function with PyRunning lock as a Task with GIL.
    /// </summary>
    /// <param name="func">Function to run.</param>
    /// <param name="waitTimeout">Time limit for waiting on PyRunning lock.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <exception cref="OperationCanceledException">cancelToken was canceled, or waitTimeout expired.</exception>
    public async Task<T> RunInThreadWithLock<T>(Func<T> func, TimeSpan? waitTimeout = null, CancellationToken cancelToken = default)
    {
        // Wait to acquire PyRunning lock
        await PyRunning.WaitAsync(cancelToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using (Py.GIL())
                {
                    return func();
                }
            }, cancelToken);
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
    public async Task RunInThreadWithLock(Action action, TimeSpan? waitTimeout = null, CancellationToken cancelToken = default)
    {
        // Wait to acquire PyRunning lock
        await PyRunning.WaitAsync(cancelToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using (Py.GIL())
                {
                    action();
                }
            }, cancelToken);
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
        var version = await RunInThreadWithLock(() =>
        {
            dynamic info = PythonEngine.Eval("tuple(__import__('sys').version_info)");
            return new PyVersionInfo(
                info[0].As<int>(),
                info[1].As<int>(),
                info[2].As<int>(),
                info[3].As<string>(),
                info[4].As<int>()
            );
        });
        return version;
    }
}
