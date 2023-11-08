using Avalonia.Controls;

namespace StabilityMatrix.UITests.Extensions;

public static class VisualExtensions
{
    public static Rect GetRelativeBounds(this Visual visual, TopLevel topLevel)
    {
        var origin =
            visual.TranslatePoint(new Point(0, 0), topLevel)
            ?? throw new NullReferenceException("Origin is null");

        var bounds = new Rect(origin, visual.Bounds.Size);

        return bounds;
    }
}
