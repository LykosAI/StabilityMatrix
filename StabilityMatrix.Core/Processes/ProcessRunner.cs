using System.Diagnostics;
using System.Text;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Processes;

public record struct ProcessResult(int ExitCode, string? StandardOutput, string? StandardError);

public static class ProcessRunner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    /// <summary>
    /// Opens the given URL in the default browser.
    /// </summary>
    /// <param name="url">URL as string</param>
    public static void OpenUrl(string url)
    {
        Logger.Debug($"Opening URL '{url}'");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    
    /// <summary>
    /// Opens the given URL in the default browser.
    /// </summary>
    /// <param name="url">URI, using AbsoluteUri component</param>
    public static void OpenUrl(Uri url)
    {
        OpenUrl(url.AbsoluteUri);
    }
    
    /// <summary>
    /// Starts and tracks a process.
    /// </summary>
    private static Process StartTrackedProcess(Process process)
    {
        process.Start();
        // Currently only supported on Windows
        if (Compat.IsWindows)
        {
            // Supress errors here since the process may have already exited
            try
            {
                ProcessTracker.AddProcess(process);
            }
            catch (InvalidOperationException)
            {
            }
        }
        return process;
    }

    public static async Task<string> GetProcessOutputAsync(string fileName, string arguments)
    {
        Logger.Debug($"Starting process '{fileName}' with arguments '{arguments}'");
        
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        
        using var process = new Process();
        process.StartInfo = info;
        StartTrackedProcess(process);

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }

    public static Process StartProcess(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        Action<ProcessOutput>? outputDataReceived = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        Logger.Debug($"Starting process '{fileName}' with arguments '{arguments}'");
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                info.EnvironmentVariables[key] = value;
            }
        }

        if (workingDirectory != null)
        {
            info.WorkingDirectory = workingDirectory;
        }

        var process = new AnsiProcess(info);
        StartTrackedProcess(process);

        if (outputDataReceived != null)
        {
            process.BeginAnsiRead(outputDataReceived);
        }

        return process;
    }

    public static Process StartProcess(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        Action<ProcessOutput>? outputDataReceived = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        // Quote arguments containing spaces
        var args = string.Join(" ", arguments
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(Quote));
        return StartProcess(fileName, args, workingDirectory, outputDataReceived, environmentVariables);
    }
    
    public static async Task<ProcessResult> RunBashCommand(string command, string workingDirectory = "")
    {
        var processInfo = new ProcessStartInfo("bash", "-c \"" + command + "\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };

        using var process = new Process();
        process.StartInfo = processInfo;
        
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) => stdout.Append(args.Data);
        process.ErrorDataReceived += (_, args) => stderr.Append(args.Data);
        
        StartTrackedProcess(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
    
    public static Task<ProcessResult> RunBashCommand(
        IEnumerable<string> commands,
        string workingDirectory = "")
    {
        // Quote arguments containing spaces
        var args = string.Join(" ", commands.Select(Quote));
        return RunBashCommand(args, workingDirectory);
    }

    /// <summary>
    /// Quotes argument with double quotes if it contains spaces,
    /// and does not already start and end with double quotes.
    /// </summary>
    public static string Quote(string argument)
    {
        var inner = argument.Trim('"');
        return inner.Contains(' ') ? $"\"{inner}\"" : argument;
    }

    /// <summary>
    /// Check if the process exited with the expected exit code.
    /// </summary>
    /// <param name="process">Process to check.</param>
    /// <param name="expectedExitCode">Expected exit code.</param>
    /// <param name="stdout">Process stdout.</param>
    /// <param name="stderr">Process stderr.</param>
    /// <exception cref="ProcessException">Thrown if exit code does not match expected value.</exception>
    // ReSharper disable once MemberCanBePrivate.Global
    public static Task ValidateExitConditionAsync(Process process, int expectedExitCode = 0, string? stdout = null, string? stderr = null)
    {
        var exitCode = process.ExitCode;
        if (exitCode == expectedExitCode)
        {
            return Task.CompletedTask;
        }

        var pName = process.StartInfo.FileName;
        var msg = $"Process {pName} failed with exit-code {exitCode}. stdout: '{stdout}', stderr: '{stderr}'";
        Logger.Error(msg);
        throw new ProcessException(msg);
    }

    /// <summary>
    /// Waits for process to exit, then validates exit code.
    /// </summary>
    /// <param name="process">Process to check.</param>
    /// <param name="expectedExitCode">Expected exit code.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <exception cref="ProcessException">Thrown if exit code does not match expected value.</exception>
    public static async Task WaitForExitConditionAsync(Process process, int expectedExitCode = 0, CancellationToken cancelToken = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) => stdout.Append(args.Data);
        process.ErrorDataReceived += (_, args) => stderr.Append(args.Data);
        await process.WaitForExitAsync(cancelToken);
        await ValidateExitConditionAsync(process, expectedExitCode, stdout.ToString(), stderr.ToString());
    }
}
