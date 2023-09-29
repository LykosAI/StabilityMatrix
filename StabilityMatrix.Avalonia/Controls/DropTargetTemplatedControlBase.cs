using Avalonia.Input;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Controls;

public abstract class DropTargetTemplatedControlBase : TemplatedControlBase
{
    protected DropTargetTemplatedControlBase()
    {
        AddHandler(DragDrop.DropEvent, DropHandler);
        AddHandler(DragDrop.DragOverEvent, DragOverHandler);

        DragDrop.SetAllowDrop(this, true);
    }

    protected virtual void DragOverHandler(object? sender, DragEventArgs e)
    {
        if (DataContext is IDropTarget dropTarget)
        {
            dropTarget.DragOver(sender, e);
        }
    }

    protected virtual void DropHandler(object? sender, DragEventArgs e)
    {
        if (DataContext is IDropTarget dropTarget)
        {
            dropTarget.Drop(sender, e);
        }
    }
}
