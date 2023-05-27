using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StabilityMatrix.Exceptions;

namespace StabilityMatrix.Helper;

public static class ProcessRunner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static async Task<string> GetProcessOutputAsync(string fileName, string arguments)
    {
        Logger.Trace($"Starting process '{fileName}' with arguments '{arguments}'");
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }

    public static Process StartProcess(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        Action<string?>? outputDataReceived = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        Logger.Trace($"Starting process '{fileName}' with arguments '{arguments}'");
        var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                process.StartInfo.EnvironmentVariables[key] = value;
            }
        }

        if (workingDirectory != null)
        {
            process.StartInfo.WorkingDirectory = workingDirectory;
        }

        if (outputDataReceived != null)
        {
            process.OutputDataReceived += (_, args) => outputDataReceived(args.Data);
            process.ErrorDataReceived += (_, args) => outputDataReceived(args.Data);
        }

        process.Start();

        if (outputDataReceived != null)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        return process;
    }

    /// <summary>
    /// Check if the process exited with the expected exit code.
    /// </summary>
    /// <param name="process">Process to check.</param>
    /// <param name="expectedExitCode">Expected exit code.</param>
    /// <exception cref="ProcessException">Thrown if exit code does not match expected value.</exception>
    public static async Task ValidateExitConditionAsync(Process process, int expectedExitCode = 0)
    {
        var exitCode = process.ExitCode;
        if (exitCode != expectedExitCode)
        {
            var pName = process.StartInfo.FileName;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            var msg = $"Process {pName} failed with exit-code {exitCode}. stdout: '{stdout}', stderr: '{stderr}'";
            Logger.Error(msg);
            throw new ProcessException(msg);
        }
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
        await process.WaitForExitAsync(cancelToken);
        await ValidateExitConditionAsync(process, expectedExitCode);
    }
}
