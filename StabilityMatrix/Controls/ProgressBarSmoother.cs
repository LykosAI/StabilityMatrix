using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System;

namespace StabilityMatrix.Controls;

public class ProgressBarSmoother
{
    public static double GetSmoothValue(DependencyObject obj)
    {
        return (double)obj.GetValue(SmoothValueProperty);
    }

    public static void SetSmoothValue(DependencyObject obj, double value)
    {
        obj.SetValue(SmoothValueProperty, value);
    }

    public static readonly DependencyProperty SmoothValueProperty =
        DependencyProperty.RegisterAttached("SmoothValue", typeof(double),
            typeof(ProgressBarSmoother), new PropertyMetadata(0.0, Changing));

    private static void Changing(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var anim = new DoubleAnimation((double)e.OldValue, (double)e.NewValue, TimeSpan.FromMilliseconds(150));
        (d as ProgressBar)?.BeginAnimation(RangeBase.ValueProperty, anim, HandoffBehavior.Compose);
    }
}
