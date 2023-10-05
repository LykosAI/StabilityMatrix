using Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Models;

public interface IPersistentViewProvider
{
    Control? AttachedPersistentView { get; set; }
}
