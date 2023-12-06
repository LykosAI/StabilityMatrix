using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// A control used to indicate the progress of an operation.
/// </summary>
[PseudoClasses(":preserveaspect", ":indeterminate")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class ProgressRing : RangeBase
{
    private Arc? fillArc;

    public static readonly StyledProperty<bool> IsIndeterminateProperty = ProgressBar
        .IsIndeterminateProperty
        .AddOwner<ProgressRing>();

    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public static readonly StyledProperty<bool> PreserveAspectProperty = AvaloniaProperty.Register<ProgressRing, bool>(
        nameof(PreserveAspect),
        true
    );

    public bool PreserveAspect
    {
        get => GetValue(PreserveAspectProperty);
        set => SetValue(PreserveAspectProperty, value);
    }

    public static readonly StyledProperty<double> StrokeThicknessProperty = Shape
        .StrokeThicknessProperty
        .AddOwner<ProgressRing>();

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public static readonly StyledProperty<double> StartAngleProperty = AvaloniaProperty.Register<ProgressRing, double>(
        nameof(StartAngle)
    );

    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public static readonly StyledProperty<double> SweepAngleProperty = AvaloniaProperty.Register<ProgressRing, double>(
        nameof(SweepAngle)
    );

    public double SweepAngle
    {
        get => GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    public static readonly StyledProperty<double> EndAngleProperty = AvaloniaProperty.Register<ProgressRing, double>(
        nameof(EndAngle),
        360
    );

    public double EndAngle
    {
        get => GetValue(EndAngleProperty);
        set => SetValue(EndAngleProperty, value);
    }

    static ProgressRing()
    {
        AffectsRender<ProgressRing>(SweepAngleProperty, StartAngleProperty, EndAngleProperty);

        ValueProperty.Changed.AddClassHandler<ProgressRing>(OnValuePropertyChanged);
        SweepAngleProperty.Changed.AddClassHandler<ProgressRing>(OnSweepAnglePropertyChanged);
    }

    public ProgressRing()
    {
        UpdatePseudoClasses(IsIndeterminate, PreserveAspect);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        fillArc = e.NameScope.Find<Arc>("PART_Fill");
        if (fillArc is not null)
        {
            fillArc.StartAngle = StartAngle;
            fillArc.SweepAngle = SweepAngle;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var e = change as AvaloniaPropertyChangedEventArgs<bool>;
        if (e is null)
            return;

        if (e.Property == IsIndeterminateProperty)
        {
            UpdatePseudoClasses(e.NewValue.GetValueOrDefault(), null);
        }
        else if (e.Property == PreserveAspectProperty)
        {
            UpdatePseudoClasses(null, e.NewValue.GetValueOrDefault());
        }
    }

    private void UpdatePseudoClasses(bool? isIndeterminate, bool? preserveAspect)
    {
        if (isIndeterminate.HasValue)
        {
            PseudoClasses.Set(":indeterminate", isIndeterminate.Value);
        }

        if (preserveAspect.HasValue)
        {
            PseudoClasses.Set(":preserveaspect", preserveAspect.Value);
        }
    }

    private static void OnValuePropertyChanged(ProgressRing sender, AvaloniaPropertyChangedEventArgs e)
    {
        sender.SweepAngle =
            ((double)e.NewValue! - sender.Minimum)
            * (sender.EndAngle - sender.StartAngle)
            / (sender.Maximum - sender.Minimum);
    }

    private static void OnSweepAnglePropertyChanged(ProgressRing sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender.fillArc is { } arc)
        {
            arc.SweepAngle = Math.Round(e.GetNewValue<double>());
        }
    }
}
