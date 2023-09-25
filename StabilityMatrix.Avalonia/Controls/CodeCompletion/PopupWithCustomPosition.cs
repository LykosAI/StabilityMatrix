using Avalonia;
using Avalonia.Controls.Primitives;

namespace StabilityMatrix.Avalonia.Controls.CodeCompletion;

internal class PopupWithCustomPosition : Popup
{
    public Point Offset
    {
        get => new(HorizontalOffset, VerticalOffset);
        set
        {
            HorizontalOffset = value.X;
            VerticalOffset = value.Y;

            // this.Revalidate(VerticalOffsetProperty);
        }
    }
}
