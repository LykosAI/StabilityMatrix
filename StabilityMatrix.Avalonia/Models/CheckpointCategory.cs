using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.Models;

public partial class CheckpointCategory : TreeViewDirectory
{
    [ObservableProperty]
    private int count;

    [ObservableProperty]
    private bool isExpanded;

    public new ObservableCollection<CheckpointCategory> SubDirectories { get; set; } = new();

    public IEnumerable<CheckpointCategory> Flatten()
    {
        yield return this;

        foreach (var subDirectory in SubDirectories)
        {
            foreach (var nestedSubDirectory in subDirectory.Flatten())
            {
                yield return nestedSubDirectory;
            }
        }
    }

    public string GetId() => $@"{Name};{Flatten().Count()}";
}
