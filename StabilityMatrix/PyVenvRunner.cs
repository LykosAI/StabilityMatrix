using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace StabilityMatrix;

/// <summary>
/// Python runner using a subprocess, mainly for venv support.
/// </summary>
public class PyVenvRunner: IDisposable
{
    public Process? Process { get; private set; }
    private ProcessStartInfo StartInfo => new()
    {
        FileName = PythonPath,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        CreateNoWindow = true
    };
    public string RootPath { get; private set; }
    
    public event EventHandler<string> OnStdoutUpdate;

    public PyVenvRunner(string path)
    {
        RootPath = path;
        OnStdoutUpdate += (_, _) => { };
    }
    
    // Whether the activate script exists
    public bool Exists() => System.IO.File.Exists(RootPath + "Scripts/activate");
    
    /// <summary>
    /// The path to the python executable.
    /// </summary>
    public string PythonPath => RootPath + @"\Scripts\python.exe";

    /// <summary>
    /// Configures the venv at path.
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

        await PyRunner.Exec(@"
import venv
venv.EnvBuilder(with_pip=True).create(r""" + RootPath + @""")
        ");
    }

    public void RunDetached(string arguments)
    {
        this.Process = new Process();
        Process.StartInfo = StartInfo;
        Process.StartInfo.Arguments = arguments;
        
        // Bind output data event
        Process.OutputDataReceived += OnProcessOutputReceived;
        
        // Start the process
        Process.Start();
        Process.BeginOutputReadLine();
    }

    /// <summary>
    /// Called on process output data.
    /// </summary>
    private void OnProcessOutputReceived(object sender, DataReceivedEventArgs e)
    {
        var data = e.Data;
        if (!string.IsNullOrEmpty(data))
        {
            OnStdoutUpdate?.Invoke(this, data);
        }
    }

    public void Dispose()
    {
        Process?.Dispose();
        GC.SuppressFinalize(this);
    }
}