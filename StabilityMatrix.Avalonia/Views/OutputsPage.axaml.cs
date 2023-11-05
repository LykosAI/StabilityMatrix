using Avalonia.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class OutputsPage : UserControlBase
{
    public OutputsPage()
    {
        InitializeComponent();
    }

    private void ScrollViewer_MouseWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control)
            return;
        if (DataContext is not OutputsPageViewModel vm)
            return;

        if (e.Delta.Y > 0)
        {
            if (vm.ImageSize.Height >= 500)
                return;
            vm.ImageSize += new Size(10, 10);
        }
        else
        {
            if (vm.ImageSize.Height <= 200)
                return;
            vm.ImageSize -= new Size(10, 10);
        }

        ImageRepeater.InvalidateArrange();
        ImageRepeater.InvalidateMeasure();

        e.Handled = true;
    }
}
