using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using Injectio.Attributes;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Controls;

[RegisterTransient<ImageFolderCard>]
public class ImageFolderCard : DropTargetTemplatedControlBase
{
    private ItemsRepeater? imageRepeater;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        imageRepeater = e.NameScope.Find<ItemsRepeater>("ImageRepeater");
        base.OnApplyTemplate(e);
    }

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

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control)
            return;
        if (DataContext is not ImageFolderCardViewModel vm)
            return;

        if (e.Delta.Y > 0)
        {
            if (vm.ImageSize.Height >= 500)
                return;
            vm.ImageSize += new Size(15, 19);
        }
        else
        {
            if (vm.ImageSize.Height <= 200)
                return;
            vm.ImageSize -= new Size(15, 19);
        }

        imageRepeater?.InvalidateArrange();
        imageRepeater?.InvalidateMeasure();

        e.Handled = true;
    }
}
