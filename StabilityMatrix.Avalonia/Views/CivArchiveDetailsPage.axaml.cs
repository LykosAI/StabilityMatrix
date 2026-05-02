using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterTransient<CivArchiveDetailsPage>]
public partial class CivArchiveDetailsPage : UserControlBase
{
    public CivArchiveDetailsPage()
    {
        InitializeComponent();
    }

    private void ImageScroller_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv)
            return;

        var scrollAmount = e.Delta.Y * 75;
        sv.Offset = new Vector(sv.Offset.X - scrollAmount, sv.Offset.Y);
        e.Handled = true;
    }
}
