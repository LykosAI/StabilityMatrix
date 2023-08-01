namespace StabilityMatrix.Core.Processes;

public readonly record struct ProcessResult
{
    public required int ExitCode { get; init; }
    public string? StandardOutput { get; init; }
    public string? StandardError { get; init; }
}
