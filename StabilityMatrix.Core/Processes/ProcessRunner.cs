using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Processes;

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

    public static async Task<string> GetProcessOutputAsync(string fileName, string arguments)
    {
        Logger.Debug($"Starting process '{fileName}' with arguments '{arguments}'");
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        ProcessTracker.AddProcess(process);

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
        var processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = fileName;
        processStartInfo.Arguments = arguments;
        processStartInfo.UseShellExecute = false;
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;
        processStartInfo.CreateNoWindow = true;

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                processStartInfo.EnvironmentVariables[key] = value;
            }
        }

        if (workingDirectory != null)
        {
            processStartInfo.WorkingDirectory = workingDirectory;
        }

        var process = new AnsiProcess(processStartInfo);

        process.Start();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ProcessTracker.AddProcess(process);
        }

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
        var args = string.Join(" ", arguments.Select(Quote));
        return StartProcess(fileName, args, workingDirectory, outputDataReceived, environmentVariables);
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
