using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.ViewModels.HuggingFacePage;

/// <summary>
/// A node in the structured (folder-tree) view of a HuggingFace repo. A node is either a
/// folder (<see cref="File"/> is null, has <see cref="Children"/>) or a file leaf
/// (wraps a <see cref="HuggingFaceFileViewModel"/>).
/// </summary>
public partial class HuggingFaceTreeNodeViewModel : ViewModelBase
{
    /// <summary>The single path segment for this node (folder or file name).</summary>
    public required string Name { get; init; }

    /// <summary>The wrapped file, or null when this node is a folder.</summary>
    public HuggingFaceFileViewModel? File { get; init; }

    public AvaloniaList<HuggingFaceTreeNodeViewModel> Children { get; } = new();

    public bool IsFolder => File is null;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool isChecked;

    /// <summary>Summary shown next to a folder, e.g. <c>"8 files · 30.5 GB"</c>.</summary>
    public string FolderSummary
    {
        get
        {
            var files = DescendantFiles().ToList();
            var totalSize = files.Sum(f => f.Size);
            var sizeText =
                totalSize > 0
                    ? $" · {StabilityMatrix.Core.Helper.Size.FormatBase10Bytes(totalSize)}"
                    : string.Empty;
            return $"{files.Count} {(files.Count == 1 ? "file" : "files")}{sizeText}";
        }
    }

    partial void OnIsCheckedChanged(bool value)
    {
        // Cascade to direct children: subfolders propagate via their own OnIsCheckedChanged,
        // so their checkboxes reflect the state too (not just the leaf files).
        foreach (var child in Children)
        {
            if (child.IsFolder)
            {
                child.IsChecked = value;
            }
            else if (child.File is { Exists: false } file)
            {
                file.IsSelected = value;
            }
        }
    }

    /// <summary>All file leaves under this node (recursively).</summary>
    public IEnumerable<HuggingFaceFileViewModel> DescendantFiles()
    {
        if (File is not null)
        {
            yield return File;
            yield break;
        }

        foreach (var child in Children)
        {
            foreach (var file in child.DescendantFiles())
                yield return file;
        }
    }
}
