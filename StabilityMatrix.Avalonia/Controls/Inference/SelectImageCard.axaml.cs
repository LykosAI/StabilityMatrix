using System;
using System.Drawing;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class SelectImageCard : DropTargetTemplatedControlBase
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (DataContext is not SelectImageCardViewModel vm)
            return;

        if (e.NameScope.Find<BetterAdvancedImage>("PART_BetterAdvancedImage") is not { } image)
            return;

        image
            .WhenPropertyChanged(x => x.CurrentImage)
            .Subscribe(propertyValue =>
            {
                if (propertyValue.Value?.Size is { } size)
                {
                    vm.CurrentBitmapSize = new Size(Convert.ToInt32(size.Width), Convert.ToInt32(size.Height));
                }
                else
                {
                    vm.CurrentBitmapSize = Size.Empty;
                }
            });
    }
}
