using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.Models;

public partial class TreeViewDirectory : ObservableObject
{
    public ObservableCollection<TreeViewDirectory> SubDirectories { get; set; } = new();
    public required string Name { get; set; }
    public required string Path { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}
