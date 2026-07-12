using Avalonia.Controls.Presenters;
using Avalonia.Input;
using StabilityMatrix.Avalonia.Helpers;

namespace StabilityMatrix.Avalonia.Controls.Scroll;

public class BetterScrollContentPresenter : ScrollContentPresenter
{
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers == PlatformKeyModifiers.CommandModifier)
            return;
        base.OnPointerWheelChanged(e);
    }
}
