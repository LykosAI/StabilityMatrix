using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// A folder node in the installed workflows tree.
/// </summary>
public partial class WorkflowCategory : TreeViewDirectory
{
    /// <summary>Number of workflows in this folder and its subfolders.</summary>
    [ObservableProperty]
    private int count;

    /// <summary>
    /// Filter this node applies to <see cref="InstalledWorkflow.Folder"/>:
    /// null shows everything, empty string shows library-root files only,
    /// otherwise the relative folder path whose subtree to show.
    /// </summary>
    public string? FilterPath { get; init; }

    public new ObservableCollection<WorkflowCategory> SubDirectories { get; set; } = [];
}
