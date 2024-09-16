using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Xaml.Interactivity;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.Behaviors;

public class ResizeBehavior : Behavior<Control>
{
    public static readonly StyledProperty<double> MinResizeFactorProperty = AvaloniaProperty.Register<
        ResizeBehavior,
        double
    >(nameof(MinResizeFactor), 0.5);

    public double MinResizeFactor
    {
        get => GetValue(MinResizeFactorProperty);
        set => SetValue(MinResizeFactorProperty, value);
    }

    public static readonly StyledProperty<double> MaxResizeFactorProperty = AvaloniaProperty.Register<
        ResizeBehavior,
        double
    >(nameof(MaxResizeFactor), 1.5);

    public double MaxResizeFactor
    {
        get => GetValue(MaxResizeFactorProperty);
        set => SetValue(MaxResizeFactorProperty, value);
    }

    public static readonly StyledProperty<double> ResizeFactorProperty = AvaloniaProperty.Register<
        ResizeBehavior,
        double
    >(nameof(ResizeFactor), 1, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceResizeFactor);

    public double ResizeFactor
    {
        get => GetValue(ResizeFactorProperty);
        set => SetValue(ResizeFactorProperty, value);
    }

    private static double CoerceResizeFactor(AvaloniaObject sender, double value)
    {
        return ValidateDouble(value)
            ? Math.Clamp(
                value,
                sender.GetValue(MinResizeFactorProperty),
                sender.GetValue(MaxResizeFactorProperty)
            )
            : sender.GetValue(ResizeFactorProperty);
    }

    public static readonly StyledProperty<double> SmallChangeProperty = AvaloniaProperty.Register<
        ResizeBehavior,
        double
    >(nameof(SmallChange), 0.05);

    public double SmallChange
    {
        get => GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public static readonly StyledProperty<bool> UseMouseWheelResizeProperty = AvaloniaProperty.Register<
        ResizeBehavior,
        bool
    >(nameof(UseMouseWheelResize), true);

    public bool UseMouseWheelResize
    {
        get => GetValue(UseMouseWheelResizeProperty);
        set => SetValue(UseMouseWheelResizeProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is null)
            return;

        AssociatedObject.PointerWheelChanged += OnPointerWheelChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (AssociatedObject is null)
            return;

        AssociatedObject.PointerWheelChanged -= OnPointerWheelChanged;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control)
            return;

        if (!UseMouseWheelResize)
            return;

        if (e.Delta.Y > 0 && ResizeFactor < MaxResizeFactor)
        {
            ResizeFactor += SmallChange;
        }
        else if (e.Delta.Y < 0 && ResizeFactor > MinResizeFactor)
        {
            ResizeFactor -= SmallChange;
        }

        e.Handled = true;

        AssociatedObject?.InvalidateMeasure();
        AssociatedObject?.InvalidateArrange();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ResizeFactorProperty)
        {
            CoerceValue(ResizeFactorProperty);
        }
    }

    private static bool ValidateDouble(double value)
    {
        return !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
