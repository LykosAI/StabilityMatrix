using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Python.Runtime;

namespace StabilityMatrix;

internal static class PyRunner
{
    private const string RelativeDllPath = @"Assets\Python310\python310.dll";
    public static string DllPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RelativeDllPath);

    public static PyIOStream StdOutStream;
    public static PyIOStream StdErrStream;

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
    
    // Evaluate Python code
    public static async Task<string> Eval(string code)
    {
        using (Py.GIL())
        {
            return await Task.Run(() =>
            {
                dynamic result = PythonEngine.Eval(code);
                return result.ToString();
            });
        }
    }
}