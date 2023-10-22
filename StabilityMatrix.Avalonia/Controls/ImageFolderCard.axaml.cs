using Avalonia.Input;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class ImageFolderCard : DropTargetTemplatedControlBase
{
    /// <inheritdoc />
    protected override void DropHandler(object? sender, DragEventArgs e)
    {
        base.DropHandler(sender, e);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void DragOverHandler(object? sender, DragEventArgs e)
    {
        base.DragOverHandler(sender, e);
        e.Handled = true;
    }
}
