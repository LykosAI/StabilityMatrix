using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NLog;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Python;

/// <summary>
/// Python runner using a subprocess, mainly for venv support.
/// </summary>
public class PyVenvRunner : IDisposable
{
    /// <summary>
    /// The process running the python executable.
    /// </summary>
    public Process? Process { get; private set; }

    /// <summary>
    /// The path to the venv root directory.
    /// </summary>
    public string RootPath { get; private set; }


    /// <summary>
    /// The path to the python executable.
    /// </summary>
    public string PythonPath => RootPath + @"\Scripts\python.exe";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public PyVenvRunner(string path)
    {
        RootPath = path;
    }

    // Whether the activate script exists
    public bool Exists() => System.IO.File.Exists(PythonPath);

    /// <summary>
    /// Creates a venv at the configured path.
    /// </summary>
    public async Task Setup(bool existsOk = false)
    {
        if (!existsOk && Exists())
        {
            throw new InvalidOperationException("Venv already exists");
        }

        // Create RootPath if it doesn't exist
        if (!System.IO.Directory.Exists(RootPath))
        {
            System.IO.Directory.CreateDirectory(RootPath);
        }

        // Create venv
        var venvProc = ProcessRunner.StartProcess(PyRunner.ExePath, $"-m virtualenv \"{RootPath}\"");
        await venvProc.WaitForExitAsync();

        // Check return code
        var returnCode = venvProc.ExitCode;
        if (returnCode != 0)
        {
            var output = await venvProc.StandardOutput.ReadToEndAsync();
            output += await venvProc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Venv creation failed with code {returnCode}: {output}");
        }
    }

    public void RunDetached(string arguments, Action<string?>? outputDataReceived, Action<int>? onExit = null,
        bool unbuffered = true, string workingDirectory = "")
    {
        if (!Exists())
        {
            throw new InvalidOperationException("Venv python process does not exist");
        }

        Logger.Debug($"Launching RunDetached at {PythonPath} with args {arguments}");

        if (unbuffered)
        {
            var env = new Dictionary<string, string>
            {
                {"PYTHONUNBUFFERED", "1"}
            };
            Process = ProcessRunner.StartProcess(PythonPath, "-u " + arguments, workingDirectory, outputDataReceived,
                env);
        }
        else
        {
            Process = ProcessRunner.StartProcess(PythonPath, arguments, outputDataReceived: outputDataReceived,
                workingDirectory: workingDirectory);
        }

        if (onExit != null)
        {
            Process.EnableRaisingEvents = true;
            Process.Exited += (_, _) => onExit(Process.ExitCode);
        }
    }

    public void Dispose()
    {
        Process?.Kill();
        GC.SuppressFinalize(this);
    }
}
