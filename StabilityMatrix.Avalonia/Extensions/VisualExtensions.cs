using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Extensions;

public static class VisualExtensions
{
    [Conditional("DEBUG")]
    public static void AttachDebugSaveScreenshot(this AppWindowBase visual)
    {
        visual.AddDisposableHandler(
            InputElement.KeyDownEvent,
            (s, e) =>
            {
                if (new KeyGesture(Key.F10).Matches(e))
                {
                    App.DebugSaveScreenshot();
                }
            },
            RoutingStrategies.Tunnel
        );
    }
}
