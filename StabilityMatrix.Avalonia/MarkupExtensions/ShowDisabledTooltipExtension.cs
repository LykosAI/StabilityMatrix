using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JetBrains.Annotations;

namespace StabilityMatrix.Avalonia.MarkupExtensions;

/// <summary>
/// Show tooltip on Controls with IsEffectivelyEnabled = false
/// https://github.com/AvaloniaUI/Avalonia/issues/3847#issuecomment-1618790059
/// </summary>
[PublicAPI]
public static class ShowDisabledTooltipExtension
{
    static ShowDisabledTooltipExtension()
    {
        ShowOnDisabledProperty.Changed.AddClassHandler<Control>(HandleShowOnDisabledChanged);
    }

    public static bool GetShowOnDisabled(AvaloniaObject obj)
    {
        return obj.GetValue(ShowOnDisabledProperty);
    }

    public static void SetShowOnDisabled(AvaloniaObject obj, bool value)
    {
        obj.SetValue(ShowOnDisabledProperty, value);
    }

    public static readonly AttachedProperty<bool> ShowOnDisabledProperty = AvaloniaProperty.RegisterAttached<
        object,
        Control,
        bool
    >("ShowOnDisabled");

    private static void HandleShowOnDisabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.GetNewValue<bool>())
        {
            control.DetachedFromVisualTree += AttachedControl_DetachedFromVisualOrExtension;
            control.AttachedToVisualTree += AttachedControl_AttachedToVisualTree;
            if (control.IsInitialized)
            {
                // enabled after visual attached
                AttachedControl_AttachedToVisualTree(control, null!);
            }
        }
        else
        {
            AttachedControl_DetachedFromVisualOrExtension(control, null!);
        }
    }

    private static void AttachedControl_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control control || TopLevel.GetTopLevel(control) is not { } tl)
        {
            return;
        }
        // NOTE pointermove needed to be tunneled for me but you may not need to...
        tl.AddHandler(InputElement.PointerMovedEvent, TopLevel_PointerMoved, RoutingStrategies.Tunnel);
    }

    private static void AttachedControl_DetachedFromVisualOrExtension(object? s, VisualTreeAttachmentEventArgs e)
    {
        if (s is not Control control)
        {
            return;
        }
        control.DetachedFromVisualTree -= AttachedControl_DetachedFromVisualOrExtension;
        control.AttachedToVisualTree -= AttachedControl_AttachedToVisualTree;
        if (TopLevel.GetTopLevel(control) is not { } tl)
        {
            return;
        }
        tl.RemoveHandler(InputElement.PointerMovedEvent, TopLevel_PointerMoved);
    }

    private static void TopLevel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control tl)
        {
            return;
        }

        var attachedControls = tl.GetVisualDescendants().Where(GetShowOnDisabled).Cast<Control>().ToList();

        // find disabled children under pointer w/ this extension enabled
        var disabledChildUnderPointer = attachedControls.FirstOrDefault(
            x =>
                x.Bounds.Contains(e.GetPosition(x.Parent as Visual))
                && x is { IsEffectivelyVisible: true, IsEffectivelyEnabled: false }
        );

        if (disabledChildUnderPointer != null)
        {
            // manually show tooltip
            ToolTip.SetIsOpen(disabledChildUnderPointer, true);
        }

        var disabledTooltipsToHide = attachedControls.Where(
            x => ToolTip.GetIsOpen(x) && x != disabledChildUnderPointer && !x.IsEffectivelyEnabled
        );

        foreach (var control in disabledTooltipsToHide)
        {
            ToolTip.SetIsOpen(control, false);
        }
    }
}
