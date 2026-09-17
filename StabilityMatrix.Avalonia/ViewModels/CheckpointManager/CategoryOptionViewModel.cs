using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels.CheckpointManager;

/// <summary>
/// One entry in the Checkpoint Manager's category filter list. <see cref="IsUncategorized"/> marks
/// the synthetic "no categories" entry.
/// </summary>
public partial class CategoryOptionViewModel : ObservableObject
{
    public required string Name { get; init; }

    public bool IsUncategorized { get; init; }

    public string? ColorHex { get; init; }

    public IBrush ColorBrush { get; init; } = Brushes.Transparent;

    [ObservableProperty]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
