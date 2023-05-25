using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;
using StabilityMatrix.Helper;

namespace StabilityMatrix;

internal record struct PyVersionInfo(int Major, int Minor, int Micro, string ReleaseLevel, int Serial);

internal static class PyRunner
{
    private const string 
    private const string RelativeDllPath = @"Assets\Python310\python310.dll";
    private const string RelativeExePath = @"Assets\Python310\python.exe";
    private const string RelativePipExePath = @"Assets\Python310\Scripts\pip.exe";
    private const string RelativeGetPipPath = @"Assets\Python310\get-pip.py";
    public static string DllPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeDllPath);
    public static string ExePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeExePath);
    public static string PipExePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativePipExePath);
    public static string GetPipPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeGetPipPath);

    public static PyIOStream? StdOutStream;
    public static PyIOStream? StdErrStream;

    private static readonly SemaphoreSlim PyRunning = new(1, 1);

    /// <summary>
    /// Initializes the Python runtime using the embedded dll.
    /// Can be called with no effect after initialization.
    /// </summary>
    /// <exception cref="FileNotFoundException"></exception>
    public static async Task Initialize()
    {
        if (PythonEngine.IsInitialized) return;

        // Check PythonDLL exists
        if (!File.Exists(DllPath))
        {
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
    public static async Task SetupPip()
    {
        Debug.WriteLine($"Process '{ExePath}' starting '{GetPipPath}'");
        var pythonProc = ProcessRunner.StartProcess(ExePath, GetPipPath);
        await pythonProc.WaitForExitAsync();
        // Check return code
        var returnCode = pythonProc.ExitCode;
        if (returnCode != 0)
        {
            var output = pythonProc.StandardOutput.ReadToEnd();
            Debug.WriteLine($"Error in get-pip.py: {output}");
            throw new InvalidOperationException($"Running get-pip.py failed with code {returnCode}: {output}");
        }
    }
    
    /// <summary>
    /// Install a Python package with pip
    /// </summary>
    public static async Task InstallPackage(string package)
    {
        var pipProc = ProcessRunner.StartProcess(PipExePath, $"install {package}");
        await pipProc.WaitForExitAsync();
        // Check return code
        var returnCode = pipProc.ExitCode;
        if (returnCode != 0)
        {
            var output = await pipProc.StandardOutput.ReadToEndAsync(); 
            throw new InvalidOperationException($"Pip install failed with code {returnCode}: {output}");
        }
    }
    
    /// <summary>
    /// Run a Function with PyRunning lock as a Task with GIL.
    /// </summary>
    /// <param name="func">Function to run.</param>
    /// <param name="waitTimeout">Time limit for waiting on PyRunning lock.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <exception cref="OperationCanceledException">cancelToken was canceled, or waitTimeout expired.</exception>
    private static async Task<T> RunInThreadWithLock<T>(Func<T> func, TimeSpan? waitTimeout = null, CancellationToken cancelToken = default)
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
    private static async Task RunInThreadWithLock(Action action, TimeSpan? waitTimeout = null, CancellationToken cancelToken = default)
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
    public static async Task<string> Eval(string expression)
    {
        return await Eval<string>(expression);
    }
    
    /// <summary>
    /// Evaluate Python expression and return its value
    /// </summary>
    /// <param name="expression"></param>
    public static Task<T> Eval<T>(string expression)
    {
        return RunInThreadWithLock(() =>
        {
            var result = PythonEngine.Eval(expression);
            return result.As<T>();
        });
    }
    
    /// <summary>
    /// Execute Python code without returning a value
    /// </summary>
    /// <param name="code"></param>
    public static Task Exec(string code)
    {
        return RunInThreadWithLock(() =>
        {
            PythonEngine.Exec(code);
        });
    }
}