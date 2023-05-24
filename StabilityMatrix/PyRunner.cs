using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Python.Runtime;

namespace StabilityMatrix;

internal static class PyRunner
{
    private const string RelativeDllPath = @"Assets\Python310\python310.dll";
    public static string DllPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeDllPath);

    public static PyIOStream StdOutStream;
    public static PyIOStream StdErrStream;

    private static readonly SemaphoreSlim PyRunning = new(1, 1);

    public static async void Initialize()
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