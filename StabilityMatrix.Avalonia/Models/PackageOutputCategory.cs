using System.Collections.ObjectModel;

namespace StabilityMatrix.Avalonia.Models;

public class PackageOutputCategory
{
    public ObservableCollection<PackageOutputCategory> SubDirectories { get; set; } = new();
    public required string Name { get; set; }
    public required string Path { get; set; }
    public int Count { get; set; }
}
