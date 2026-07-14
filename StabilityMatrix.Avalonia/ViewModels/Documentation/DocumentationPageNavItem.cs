namespace StabilityMatrix.Avalonia.ViewModels.Documentation;

/// <summary>
/// A single navigable documentation page entry (leaf) in the sidebar tree.
/// </summary>
public partial class DocumentationPageNavItem : DocumentationNavNode
{
    /// <summary>Display title, e.g. "Overview".</summary>
    public required string Title { get; init; }

    /// <summary>Path relative to the docs root, e.g. <c>getting-started/overview.md</c>.</summary>
    public required string Path { get; init; }
}
