using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class BetterFlyout : Flyout
{
    public static readonly StyledProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty = AvaloniaProperty.Register<BetterFlyout, ScrollBarVisibility>(
        "VerticalScrollBarVisibility");

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    public static readonly StyledProperty<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty = AvaloniaProperty.Register<BetterFlyout, ScrollBarVisibility>(
        "HorizontalScrollBarVisibility");

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }
    
    protected override void OnOpened()
    {
        base.OnOpened();
        var presenter = Popup.Child;
        if (presenter.FindDescendantOfType<ScrollViewer>() is { } scrollViewer)
        {
            scrollViewer.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
            scrollViewer.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
        }
    }
}
