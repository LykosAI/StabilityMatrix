using StabilityMatrix.Core.Exceptions;

namespace StabilityMatrix.Core.Processes;

public readonly record struct ProcessResult
{
    public required int ExitCode { get; init; }
    public string? StandardOutput { get; init; }
    public string? StandardError { get; init; }

    public string? ProcessName { get; init; }

    public TimeSpan Elapsed { get; init; }

    public bool IsSuccessExitCode => ExitCode == 0;

    public void EnsureSuccessExitCode()
    {
        if (!IsSuccessExitCode)
        {
            throw new ProcessException(this);
        }
    }
}
