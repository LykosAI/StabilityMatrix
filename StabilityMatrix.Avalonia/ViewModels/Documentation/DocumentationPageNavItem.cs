using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels.Documentation;

/// <summary>
/// A single navigable documentation page entry in the sidebar.
/// </summary>
public partial class DocumentationPageNavItem : ObservableObject
{
    /// <summary>Display title, e.g. "Overview".</summary>
    public required string Title { get; init; }

    /// <summary>Path relative to the docs root, e.g. <c>getting-started/overview.md</c>.</summary>
    public required string Path { get; init; }

    [ObservableProperty]
    private bool isSelected;
}
