using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;
using StabilityMatrix.Helper;

namespace StabilityMatrix;

internal static class PyRunner
{
    private const string RelativeDllPath = @"Assets\Python310\python310.dll";
    private const string RelativeExePath = @"Assets\Python310\python.exe";
    private const string RelativePipExePath = @"Assets\Python310\Scripts\pip.exe";
    private const string RelativeGetPipPath = @"Assets\Python310\get-pip.py";
    public static string DllPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeDllPath);
    public static string ExePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeExePath);
    public static string PipExePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativePipExePath);
    public static string GetPipPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeGetPipPath);

    public static PyIOStream StdOutStream;
    public static PyIOStream StdErrStream;

    private static readonly SemaphoreSlim PyRunning = new(1, 1);

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
        
        await RedirectPythonOutput();
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
    
    // Redirect Python output
    private static async Task RedirectPythonOutput()
    {
        StdOutStream = new PyIOStream();
        StdErrStream = new PyIOStream();

        await PyRunning.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using (Py.GIL())
                {
                    dynamic sys = Py.Import("sys");
                    sys.stdout = StdOutStream;
                    sys.stderr = StdErrStream;
                }
            });
        }
        finally
        {
            PyRunning.Release();
        }
    }
    
    /// <summary>
    /// Evaluate Python expression and return its value as a string
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static async Task<string> Eval(string code)
    {
        await PyRunning.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                using (Py.GIL())
                {
                    dynamic result = PythonEngine.Eval(code);
                    return result.ToString();
                }
            });
        }
        finally
        {
            PyRunning.Release();
        }
    }
    
    /// <summary>
    /// Execute Python code without returning a value
    /// </summary>
    /// <param name="code"></param>
    public static async Task Exec(string code)
    {
        await PyRunning.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using (Py.GIL())
                {
                    PythonEngine.Exec(code);
                }
            });
        }
        finally
        {
            PyRunning.Release();
        }
    }
}