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
            $"Process {processResult.ProcessName} exited with code {processResult.ExitCode}. {{StdOut = {processResult.StandardOutput}, StdErr = {processResult.StandardError}}}"
        )
    {
        ProcessResult = processResult;
    }
}
