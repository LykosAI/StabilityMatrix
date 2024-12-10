using System;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DynamicData.Binding;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using Injectio.Attributes;
using Size = Avalonia.Size;

namespace StabilityMatrix.Avalonia.Controls;

[RegisterTransient<SelectImageCard>]
public class SelectImageCard : DropTargetTemplatedControlBase
{
    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (DataContext is not SelectImageCardViewModel vm)
            return;

        if (e.NameScope.Find<BetterAdvancedImage>("PART_BetterAdvancedImage") is not { } imageControl)
            return;

        imageControl
            .WhenPropertyChanged(x => x.CurrentImage)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(propertyValue =>
            {
                if (propertyValue.Value is { } image)
                {
                    // Sometimes Avalonia Bitmap.Size getter throws a NullReferenceException depending on skia lifetimes (probably)
                    // so just catch it and ignore it
                    Size? size = null;
                    try
                    {
                        size = image.Size;
                    }
                    catch (NullReferenceException) { }

                    if (size is not null)
                    {
                        vm.CurrentBitmapSize = new System.Drawing.Size(
                            Convert.ToInt32(size.Value.Width),
                            Convert.ToInt32(size.Value.Height)
                        );
                        return;
                    }
                }

                vm.CurrentBitmapSize = System.Drawing.Size.Empty;
            });
    }
}
