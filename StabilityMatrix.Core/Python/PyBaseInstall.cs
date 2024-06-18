using System.Collections.Immutable;
using System.Text.Json;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Python;

public class PyBaseInstall(DirectoryPath rootPath)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Root path of the Python installation.
    /// </summary>
    public DirectoryPath RootPath { get; } = rootPath;

    /// <summary>
    /// Whether this is a portable Windows installation.
    /// Path structure is different.
    /// </summary>
    public bool IsWindowsPortable { get; init; }

    private int MajorVersion { get; init; }

    private int MinorVersion { get; init; }

    public FilePath PythonExePath =>
        Compat.Switch(
            (PlatformKind.Windows, RootPath.JoinFile("python.exe")),
            (PlatformKind.Linux, RootPath.JoinFile("bin", "python3")),
            (PlatformKind.MacOS, RootPath.JoinFile("bin", "python3"))
        );

    public string DefaultTclTkPath =>
        Compat.Switch(
            (PlatformKind.Windows, RootPath.JoinFile("tcl", "tcl8.6")),
            (PlatformKind.Linux, RootPath.JoinFile("lib", "tcl8.6")),
            (PlatformKind.MacOS, RootPath.JoinFile("lib", "tcl8.6"))
        );

    /// <summary>
    /// Creates a new virtual environment runner.
    /// </summary>
    /// <param name="venvPath">Root path of the venv</param>
    public PyVenvRunner CreateVenvRunner(DirectoryPath venvPath)
    {
        return new PyVenvRunner(RootPath, venvPath);
    }

    /// <summary>
    /// Creates a new virtual environment runner.
    /// </summary>
    /// <param name="venvPath">Root path of the venv</param>
    /// <param name="withTclTkEnv">Whether to include the Tcl/Tk library paths via <see cref="TryQueryTclTkLibraryAsync"/></param>
    public async Task<PyVenvRunner> CreateVenvRunnerAsync(DirectoryPath venvPath, bool withTclTkEnv = false)
    {
        var runner = CreateVenvRunner(venvPath);

        if (withTclTkEnv)
        {
            var queryResult = await TryQueryTclTkLibraryAsync().ConfigureAwait(false);
            if (queryResult is { Result: { } result })
            {
                var env =
                    runner.EnvironmentVariables?.ToImmutableDictionary()
                    ?? ImmutableDictionary<string, string>.Empty;

                if (!string.IsNullOrEmpty(result.TclLibrary))
                {
                    env = env.SetItem("TCL_LIBRARY", result.TclLibrary);
                }
                if (!string.IsNullOrEmpty(result.TkLibrary))
                {
                    env = env.SetItem("TK_LIBRARY", result.TkLibrary);
                }

                runner.EnvironmentVariables = env;
            }
            else
            {
                Logger.Error(queryResult.Exception, "Failed to query Tcl/Tk library paths");
            }
        }

        return runner;
    }

    public async Task<TaskResult<QueryTclTkLibraryResult>> TryQueryTclTkLibraryAsync()
    {
        var processResult = await QueryTclTkLibraryPathAsync().ConfigureAwait(false);

        if (!processResult.IsSuccessExitCode || string.IsNullOrEmpty(processResult.StandardOutput))
        {
            return TaskResult<QueryTclTkLibraryResult>.FromException(new ProcessException(processResult));
        }

        try
        {
            var result = JsonSerializer.Deserialize(
                processResult.StandardOutput,
                QueryTclTkLibraryResultJsonContext.Default.QueryTclTkLibraryResult
            );

            return new TaskResult<QueryTclTkLibraryResult>(result!);
        }
        catch (JsonException e)
        {
            return TaskResult<QueryTclTkLibraryResult>.FromException(e);
        }
    }

    private async Task<ProcessResult> QueryTclTkLibraryPathAsync()
    {
        const string script = """
                              import tkinter
                              import json
                              
                              root = tkinter.Tk()
                              
                              print(json.dumps({
                                  'TclLibrary': root.tk.exprstring('$tcl_library'),
                                  'TkLibrary': root.tk.exprstring('$tk_library')
                              }))
                              """;

        return await ProcessRunner.GetProcessResultAsync(PythonExePath, ["-c", script]).ConfigureAwait(false);
    }
}
