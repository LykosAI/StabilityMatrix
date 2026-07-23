namespace StabilityMatrix.Core.Models.Documentation;

/// <summary>
/// A single documentation page discovered in the docs tree.
/// </summary>
public record DocumentationPage
{
    /// <summary>
    /// Path relative to the docs root, e.g. <c>getting-started/overview.md</c> or <c>README.md</c>.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>Humanized display title, e.g. "Overview".</summary>
    public required string Title { get; init; }
}
