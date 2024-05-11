using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.Models;

public partial class PackageOutputCategory : ObservableObject
{
    public ObservableCollection<PackageOutputCategory> SubDirectories { get; set; } = new();
    public required string Name { get; set; }
    public required string Path { get; set; }

    [ObservableProperty]
    private int count;
}
