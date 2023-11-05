using Avalonia.Controls.Presenters;
using Avalonia.Input;

namespace StabilityMatrix.Avalonia.Controls.Scroll;

public class BetterScrollContentPresenter : ScrollContentPresenter
{
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
            return;
        base.OnPointerWheelChanged(e);
    }
}
