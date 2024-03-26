using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Avalonia.Xaml.Interactivity;

namespace StabilityMatrix.Avalonia.Controls;

public class BetterContextDragBehavior : Behavior<Control>
{
    private Point _dragStartPoint;
    private PointerEventArgs? _triggerEvent;
    private bool _lock;
    private bool _captured;

    public static readonly StyledProperty<object?> ContextProperty = AvaloniaProperty.Register<
        ContextDragBehavior,
        object?
    >(nameof(Context));

    public static readonly StyledProperty<IDragHandler?> HandlerProperty = AvaloniaProperty.Register<
        ContextDragBehavior,
        IDragHandler?
    >(nameof(Handler));

    public static readonly StyledProperty<double> HorizontalDragThresholdProperty = AvaloniaProperty.Register<
        ContextDragBehavior,
        double
    >(nameof(HorizontalDragThreshold), 3);

    public static readonly StyledProperty<double> VerticalDragThresholdProperty = AvaloniaProperty.Register<
        ContextDragBehavior,
        double
    >(nameof(VerticalDragThreshold), 3);

    public static readonly StyledProperty<string> DataFormatProperty = AvaloniaProperty.Register<
        BetterContextDragBehavior,
        string
    >("DataFormat");

    public string DataFormat
    {
        get => GetValue(DataFormatProperty);
        set => SetValue(DataFormatProperty, value);
    }

    public object? Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    public IDragHandler? Handler
    {
        get => GetValue(HandlerProperty);
        set => SetValue(HandlerProperty, value);
    }

    public double HorizontalDragThreshold
    {
        get => GetValue(HorizontalDragThresholdProperty);
        set => SetValue(HorizontalDragThresholdProperty, value);
    }

    public double VerticalDragThreshold
    {
        get => GetValue(VerticalDragThresholdProperty);
        set => SetValue(VerticalDragThresholdProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(
            InputElement.PointerPressedEvent,
            AssociatedObject_PointerPressed,
            RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
        AssociatedObject?.AddHandler(
            InputElement.PointerReleasedEvent,
            AssociatedObject_PointerReleased,
            RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
        AssociatedObject?.AddHandler(
            InputElement.PointerMovedEvent,
            AssociatedObject_PointerMoved,
            RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
        AssociatedObject?.AddHandler(
            InputElement.PointerCaptureLostEvent,
            AssociatedObject_CaptureLost,
            RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble
        );
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed);
        AssociatedObject?.RemoveHandler(InputElement.PointerReleasedEvent, AssociatedObject_PointerReleased);
        AssociatedObject?.RemoveHandler(InputElement.PointerMovedEvent, AssociatedObject_PointerMoved);
        AssociatedObject?.RemoveHandler(InputElement.PointerCaptureLostEvent, AssociatedObject_CaptureLost);
    }

    private async Task DoDragDrop(PointerEventArgs triggerEvent, object? value)
    {
        var data = new DataObject();
        data.Set(DataFormat, value!);

        var effect = DragDropEffects.None;

        if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            effect |= DragDropEffects.Link;
        }
        else if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            effect |= DragDropEffects.Move;
        }
        else if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            effect |= DragDropEffects.Copy;
        }
        else
        {
            effect |= DragDropEffects.Move;
        }

        await DragDrop.DoDragDrop(triggerEvent, data, effect);
    }

    private void Released()
    {
        _triggerEvent = null;
        _lock = false;
    }

    private void AssociatedObject_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (properties.IsLeftButtonPressed)
        {
            if (e.Source is Control control && AssociatedObject?.DataContext == control.DataContext)
            {
                _dragStartPoint = e.GetPosition(null);
                _triggerEvent = e;
                _lock = true;
                _captured = true;
            }
        }
    }

    private void AssociatedObject_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_captured)
        {
            if (e.InitialPressMouseButton == MouseButton.Left && _triggerEvent is { })
            {
                Released();
            }

            _captured = false;
        }
    }

    private async void AssociatedObject_PointerMoved(object? sender, PointerEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (_captured && properties.IsLeftButtonPressed && _triggerEvent is { })
        {
            var point = e.GetPosition(null);
            var diff = _dragStartPoint - point;
            var horizontalDragThreshold = HorizontalDragThreshold;
            var verticalDragThreshold = VerticalDragThreshold;

            if (Math.Abs(diff.X) > horizontalDragThreshold || Math.Abs(diff.Y) > verticalDragThreshold)
            {
                if (_lock)
                {
                    _lock = false;
                }
                else
                {
                    return;
                }

                var context = Context ?? AssociatedObject?.DataContext;

                Handler?.BeforeDragDrop(sender, _triggerEvent, context);

                await DoDragDrop(_triggerEvent, context);

                Handler?.AfterDragDrop(sender, _triggerEvent, context);

                _triggerEvent = null;
            }
        }
    }

    private void AssociatedObject_CaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        Released();
        _captured = false;
    }
}
