using System.Collections.Generic;

namespace StabilityMatrix.Avalonia.ViewModels.Documentation;

/// <summary>
/// A section grouping in the documentation sidebar (e.g. "Getting Started").
/// </summary>
public partial class DocumentationSectionNavItem : DocumentationNavNode
{
    public DocumentationSectionNavItem()
    {
        // Sections are expanded by default.
        IsExpanded = true;
    }

    /// <summary>Section title. Empty for the root section (renders without a header).</summary>
    public required string Title { get; init; }

    /// <summary>Whether this section has a visible header (i.e. is not the root section).</summary>
    public bool HasHeader => !string.IsNullOrEmpty(Title);

    /// <summary>Pages within this section.</summary>
    public required IReadOnlyList<DocumentationPageNavItem> Pages { get; init; }
}
