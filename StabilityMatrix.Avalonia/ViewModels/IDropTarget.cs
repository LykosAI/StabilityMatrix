using Avalonia.Input;

namespace StabilityMatrix.Avalonia.ViewModels;

public interface IDropTarget
{
    void DragOver(object? sender, DragEventArgs e);
    void Drop(object? sender, DragEventArgs e);
}
