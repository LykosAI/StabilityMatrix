using System.Collections.Generic;

namespace StabilityMatrix.Core.Models.Documentation;

/// <summary>
/// A group of documentation pages that share a section folder (e.g. "Getting Started").
/// </summary>
public record DocumentationSection
{
    /// <summary>Humanized section title, e.g. "Getting Started".</summary>
    public required string Title { get; init; }

    /// <summary>
    /// The raw section folder name relative to the docs root, e.g. <c>getting-started</c>.
    /// Empty string for the root-level section.
    /// </summary>
    public required string FolderName { get; init; }

    /// <summary>Pages within this section, in listing order.</summary>
    public IReadOnlyList<DocumentationPage> Pages { get; init; } = [];
}
