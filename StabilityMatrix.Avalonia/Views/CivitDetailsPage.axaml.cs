using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views;

[RegisterTransient<CivitDetailsPage>]
public partial class CivitDetailsPage : UserControlBase
{
    public CivitDetailsPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InputElement_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv)
            return;

        var scrollAmount = e.Delta.Y * 75;
        sv.Offset = new Vector(sv.Offset.X - scrollAmount, sv.Offset.Y);
        e.Handled = true;
    }
}
