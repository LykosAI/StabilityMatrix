namespace StabilityMatrix.Core.Models;

/// <summary>
/// Union of either Tag or Branch + CommitSha.
/// </summary>
public record GitVersion : IFormattable
{
    public string? Tag { get; init; }

    public string? Branch { get; init; }

    public string? CommitSha { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Tag))
        {
            return Tag;
        }

        if (!string.IsNullOrEmpty(Branch) && !string.IsNullOrEmpty(CommitSha))
        {
            return $"{Branch}@{CommitSha[..7]}";
        }

        if (!string.IsNullOrEmpty(Branch))
        {
            return Branch;
        }

        return !string.IsNullOrEmpty(CommitSha) ? CommitSha[..7] : "";
    }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString();
    }
}
