using System.Collections.Immutable;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Python;

public interface IPyVenvRunner
{
    PyBaseInstall BaseInstall { get; }

    /// <summary>
    /// The process running the python executable.
    /// </summary>
    AnsiProcess? Process { get; }

    /// <summary>
    /// The path to the venv root directory.
    /// </summary>
    DirectoryPath RootPath { get; }

    /// <summary>
    /// Optional working directory for the python process.
    /// </summary>
    DirectoryPath? WorkingDirectory { get; set; }

    /// <summary>
    /// Optional environment variables for the python process.
    /// </summary>
    ImmutableDictionary<string, string> EnvironmentVariables { get; set; }

    /// <summary>
    /// The full path to the python executable.
    /// </summary>
    FilePath PythonPath { get; }

    /// <summary>
    /// The full path to the pip executable.
    /// </summary>
    FilePath PipPath { get; }

    /// <summary>
    /// The Python version of this venv
    /// </summary>
    PyVersion Version { get; }

    /// <summary>
    /// List of substrings to suppress from the output.
    /// When a line contains any of these substrings, it will not be forwarded to callbacks.
    /// A corresponding Info log will be written instead.
    /// </summary>
    List<string> SuppressOutput { get; }

    void UpdateEnvironmentVariables(
        Func<ImmutableDictionary<string, string>, ImmutableDictionary<string, string>> env
    );

    /// <returns>True if the venv has a Scripts\python.exe file</returns>
    bool Exists();

    /// <summary>
    /// Creates a venv at the configured path.
    /// </summary>
    Task Setup(
        bool existsOk = false,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Run a pip install command. Waits for the process to exit.
    /// workingDirectory defaults to RootPath.
    /// </summary>
    Task PipInstall(ProcessArgs args, Action<ProcessOutput>? outputDataReceived = null);

    /// <summary>
    /// Run a pip uninstall command. Waits for the process to exit.
    /// workingDirectory defaults to RootPath.
    /// </summary>
    Task PipUninstall(ProcessArgs args, Action<ProcessOutput>? outputDataReceived = null);

    /// <summary>
    /// Run a pip list command, return results as PipPackageInfo objects.
    /// </summary>
    Task<IReadOnlyList<PipPackageInfo>> PipList();

    /// <summary>
    /// Run a pip show command, return results as PipPackageInfo objects.
    /// </summary>
    Task<PipShowResult?> PipShow(string packageName);

    /// <summary>
    /// Run a pip index command, return result as PipIndexResult.
    /// </summary>
    Task<PipIndexResult?> PipIndex(string packageName, string? indexUrl = null);

    /// <summary>
    /// Run a custom install command. Waits for the process to exit.
    /// workingDirectory defaults to RootPath.
    /// </summary>
    Task CustomInstall(ProcessArgs args, Action<ProcessOutput>? outputDataReceived = null);

    /// <summary>
    /// Run a command using the venv Python executable and return the result.
    /// </summary>
    /// <param name="arguments">Arguments to pass to the Python executable.</param>
    Task<ProcessResult> Run(ProcessArgs arguments);

    void RunDetached(
        ProcessArgs args,
        Action<ProcessOutput>? outputDataReceived,
        Action<int>? onExit = null,
        bool unbuffered = true
    );

    /// <summary>
    /// Get entry points for a package.
    /// https://packaging.python.org/en/latest/specifications/entry-points/#entry-points
    /// </summary>
    Task<string?> GetEntryPoint(string entryPointName);

    /// <summary>
    /// Kills the running process and cancels stream readers, does not wait for exit.
    /// </summary>
    void Dispose();

    /// <summary>
    /// Kills the running process, waits for exit.
    /// </summary>
    ValueTask DisposeAsync();
}
