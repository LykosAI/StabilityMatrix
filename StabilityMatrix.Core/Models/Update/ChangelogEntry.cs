using Semver;

namespace StabilityMatrix.Core.Models.Update;

/// <summary>
/// A single parsed version block from the Keep-a-Changelog markdown file.
/// </summary>
public record ChangelogEntry
{
    /// <summary>
    /// Parsed semantic version of this changelog block.
    /// </summary>
    public required SemVersion Version { get; init; }

    /// <summary>
    /// The full markdown for this block, including the version heading.
    /// </summary>
    public required string Markdown { get; init; }
}
