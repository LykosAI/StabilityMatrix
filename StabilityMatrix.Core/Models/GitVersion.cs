namespace StabilityMatrix.Core.Models;

/// <summary>
/// Union of either Tag or Branch + CommitSha.
/// </summary>
public record GitVersion
{
    public string? Tag { get; init; }

    public string? Branch { get; init; }

    public string? CommitSha { get; init; }
}
