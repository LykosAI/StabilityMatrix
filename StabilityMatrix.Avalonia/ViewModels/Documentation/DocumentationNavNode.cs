using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels.Documentation;

/// <summary>
/// Base for nodes shown in the documentation navigation tree (sections and pages).
/// Exposes the expansion state consumed by the TreeView's <c>TreeViewItem</c> style binding.
/// </summary>
public abstract partial class DocumentationNavNode : ObservableObject
{
    /// <summary>Whether the corresponding <c>TreeViewItem</c> is expanded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}
