using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace StabilityMatrix.UITests.Extensions;

/// <summary>
/// Window extensions for UI tests
/// </summary>
public static class WindowExtensions
{
    public static void ClickTarget(this TopLevel topLevel, Control target)
    {
        // Check target is part of the visual tree
        var targetVisualRoot = target.GetVisualRoot();
        if (targetVisualRoot is not TopLevel)
        {
            throw new ArgumentException("Target is not part of the visual tree");
        }
        if (targetVisualRoot.Equals(topLevel))
        {
            throw new ArgumentException(
                "Target is not part of the same visual tree as the top level"
            );
        }

        var point =
            target.TranslatePoint(
                new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
                topLevel
            ) ?? throw new NullReferenceException("Point is null");

        topLevel.MouseMove(point);
        topLevel.MouseDown(point, MouseButton.Left);
        topLevel.MouseUp(point, MouseButton.Left);

        // Return mouse to outside of window
        topLevel.MouseMove(new Point(-50, -50));
    }

    public static async Task ClickTargetAsync(this TopLevel topLevel, Control target)
    {
        // Check target is part of the visual tree
        var targetVisualRoot = target.GetVisualRoot();
        if (targetVisualRoot is not TopLevel)
        {
            throw new ArgumentException("Target is not part of the visual tree");
        }
        if (!targetVisualRoot.Equals(topLevel))
        {
            throw new ArgumentException(
                "Target is not part of the same visual tree as the top level"
            );
        }

        var point =
            target.TranslatePoint(
                new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
                topLevel
            ) ?? throw new NullReferenceException("Point is null");

        topLevel.MouseMove(point);
        topLevel.MouseDown(point, MouseButton.Left);
        topLevel.MouseUp(point, MouseButton.Left);

        await Task.Delay(40);

        // Return mouse to outside of window
        topLevel.MouseMove(new Point(-50, -50));

        Dispatcher.UIThread.Invoke(() => Dispatcher.UIThread.RunJobs());
    }
}
