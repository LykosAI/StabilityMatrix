using System.Diagnostics;
using System.Text;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Exceptions;

/// <summary>
/// Exception that is thrown when a process fails.
/// </summary>
public class ProcessException : Exception
{
    public ProcessResult? ProcessResult { get; }

    public ProcessException(string message)
        : base(message) { }

    public ProcessException(ProcessResult processResult)
        : base(
            $"Process {processResult.ProcessName} exited with code {processResult.ExitCode}. "
                + $"{{StdOut = {processResult.StandardOutput}, StdErr = {processResult.StandardError}}}"
        )
    {
        ProcessResult = processResult;
    }

    public static void ThrowIfNonZeroExitCode(ProcessResult processResult)
    {
        if (processResult.IsSuccessExitCode)
            return;

        throw new ProcessException(processResult);
    }

    public static void ThrowIfNonZeroExitCode(Process process, string output)
    {
        if (!process.HasExited || process.ExitCode == 0)
            return;

        throw new ProcessException(
            new ProcessResult
            {
                ProcessName = process.StartInfo.FileName,
                ExitCode = process.ExitCode,
                StandardOutput = output
            }
        );
    }

    public static void ThrowIfNonZeroExitCode(Process process, StringBuilder outputBuilder)
    {
        if (!process.HasExited || process.ExitCode == 0)
            return;

        throw new ProcessException(
            new ProcessResult
            {
                ProcessName = process.StartInfo.FileName,
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString()
            }
        );
    }

    public static void ThrowIfNonZeroExitCode(Process process, string stdOut, string stdErr)
    {
        if (!process.HasExited || process.ExitCode == 0)
            return;

        throw new ProcessException(
            new ProcessResult
            {
                ProcessName = process.StartInfo.FileName,
                ExitCode = process.ExitCode,
                StandardOutput = stdOut,
                StandardError = stdErr
            }
        );
    }
}
