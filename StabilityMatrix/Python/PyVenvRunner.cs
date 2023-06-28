using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using Salaros.Configuration;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;

namespace StabilityMatrix.Python;

/// <summary>
/// Python runner using a subprocess, mainly for venv support.
/// </summary>
public class PyVenvRunner : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

    /// <summary>
    /// The path to the pip executable.
    /// </summary>
    public string PipPath => RootPath + @"\Scripts\pip.exe";

    /// <summary>
    /// List of substrings to suppress from the output.
    /// When a line contains any of these substrings, it will not be forwarded to callbacks.
    /// A corresponding Info log will be written instead.
    /// </summary>
    public List<string> SuppressOutput { get; } = new() { "fatal: not a git repository" };
    
    public PyVenvRunner(string path)
    {
        RootPath = path;
    }

    // Whether the activate script exists
    public bool Exists() => File.Exists(PythonPath);

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
        Directory.CreateDirectory(RootPath);

        // Create venv
        var venvProc = ProcessRunner.StartProcess(PyRunner.PythonExePath, $"-m virtualenv --always-copy \"{RootPath}\"");
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

    /// <summary>
    /// Return the pip install command for torch, automatically chooses between Cuda and CPU.
    /// </summary>
    /// <returns></returns>
    public string GetTorchInstallCommand()
    {
        if (HardwareHelper.HasNvidiaGpu())
        {
            return "torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu118";
        }

        return "torch torchvision torchaudio";
    }

    /// <summary>
    /// Install torch with pip, automatically chooses between Cuda and CPU.
    /// </summary>
    public async Task InstallTorch(Action<string?>? outputDataReceived = null)
    {
        await PipInstall(GetTorchInstallCommand(), outputDataReceived: outputDataReceived);
    }

    /// <summary>
    /// Set current python path to pyvenv.cfg
    /// This should be called before using the venv, in case user moves the venv directory.
    /// </summary>
    private void SetPyvenvCfg(string pythonDirectory)
    {
        // Skip if we are not created yet
        if (!Exists()) return;

        // Path to pyvenv.cfg
        var cfgPath = Path.Combine(RootPath, "pyvenv.cfg");
        if (!File.Exists(cfgPath))
        {
            throw new FileNotFoundException("pyvenv.cfg not found", cfgPath);
        }
        
        Logger.Info("Updating pyvenv.cfg with embedded Python directory {PyDir}", pythonDirectory);
        
        // Insert a top section
        var topSection = "[top]" + Environment.NewLine;
        var cfg = new ConfigParser(topSection + File.ReadAllText(cfgPath));
        
        // Need to set all path keys - home, base-prefix, base-exec-prefix, base-executable
        cfg.SetValue("top", "home", pythonDirectory);
        cfg.SetValue("top", "base-prefix", pythonDirectory);
        cfg.SetValue("top", "base-exec-prefix", pythonDirectory);
        cfg.SetValue("top", "base-executable", Path.Combine(pythonDirectory, "python.exe"));
        
        // Convert to string for writing, strip the top section
        var cfgString = cfg.ToString().Replace(topSection, "");
        File.WriteAllText(cfgPath, cfgString);
    }
    
    /// <summary>
    /// Run a pip install command. Waits for the process to exit.
    /// workingDirectory defaults to RootPath.
    /// </summary>
    public async Task PipInstall(string args, string? workingDirectory = null, Action<string?>? outputDataReceived = null)
    {
        if (!File.Exists(PipPath))
        {
            throw new FileNotFoundException("pip not found", PipPath);
        }
        SetPyvenvCfg(PyRunner.PythonDir);
        Process = ProcessRunner.StartProcess(PythonPath, $"-m pip install {args}", workingDirectory ?? RootPath, outputDataReceived);
        await ProcessRunner.WaitForExitConditionAsync(Process);
    }

    public void RunDetached(string arguments, Action<string?>? outputDataReceived, Action<int>? onExit = null,
        bool unbuffered = true, string workingDirectory = "")
    {
        if (!Exists())
        {
            throw new InvalidOperationException("Venv python process does not exist");
        }
        SetPyvenvCfg(PyRunner.PythonDir);
        
        Logger.Debug($"Launching RunDetached at {PythonPath} with args {arguments}");

        var filteredOutput = outputDataReceived == null ? null : new Action<string?>(s =>
        {
            if (s == null) return;
            if (SuppressOutput.Any(s.Contains))
            {
                Logger.Info("Filtered output: {S}", s);
                return;
            }
            outputDataReceived?.Invoke(s);
        });

        if (unbuffered)
        {
            var env = new Dictionary<string, string>
            {
                {"PYTHONUNBUFFERED", "1"}
            };
            Process = ProcessRunner.StartProcess(PythonPath, "-u " + arguments, workingDirectory, filteredOutput,
                env);
        }
        else
        {
            Process = ProcessRunner.StartProcess(PythonPath, arguments, outputDataReceived: filteredOutput,
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
