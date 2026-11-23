using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// A labelled start/end fraction of sampling steps (0 to 1), edited with two percent boxes and a range slider.
/// Both values stay within 0 to 1, and <see cref="Start"/> never ends up past <see cref="End"/>.
/// </summary>
public class StepRangeEditor : TemplatedControl
{
    private NumberBox? startBox;
    private NumberBox? endBox;

    public static readonly StyledProperty<string?> HeaderProperty = AvaloniaProperty.Register<
        StepRangeEditor,
        string?
    >(nameof(Header));

    /// <summary>Label shown to the left of the boxes.</summary>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<double> StartProperty = AvaloniaProperty.Register<
        StepRangeEditor,
        double
    >(nameof(Start), defaultBindingMode: BindingMode.TwoWay, coerce: (o, value) =>
        ((StepRangeEditor)o).CoerceFraction(StartProperty, value)
    );

    /// <summary>Fraction of steps where the range starts, at most <see cref="End"/>.</summary>
    public double Start
    {
        get => GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    public static readonly StyledProperty<double> EndProperty = AvaloniaProperty.Register<
        StepRangeEditor,
        double
    >(nameof(End), 1d, defaultBindingMode: BindingMode.TwoWay, coerce: (o, value) =>
        ((StepRangeEditor)o).CoerceFraction(EndProperty, value)
    );

    /// <summary>Fraction of steps where the range ends, at least <see cref="Start"/>.</summary>
    public double End
    {
        get => GetValue(EndProperty);
        set => SetValue(EndProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        startBox = e.NameScope.Find<NumberBox>("PART_StartBox");
        endBox = e.NameScope.Find<NumberBox>("PART_EndBox");
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Moving one end past the other carries the other along, like the slider's thumbs. Pushing rather
        // than clamping keeps any order of setting both (e.g. loading a saved project) landing on the new pair.
        if (change.Property == StartProperty && Start > End)
        {
            End = Start;
        }
        else if (change.Property == EndProperty && End < Start)
        {
            Start = End;
        }
    }

    /// <summary>Clamps to 0..1. A cleared box (NaN) keeps the current value.</summary>
    private double CoerceFraction(StyledProperty<double> property, double value)
    {
        var coerced = double.IsNaN(value) ? GetValue(property) : Math.Clamp(value, 0d, 1d);

        // Avalonia doesn't write a coerced value back through a two-way binding, so the source and the box
        // would keep what was rejected. Sync both once the coerced value has landed.
        if (!coerced.Equals(value))
        {
            Dispatcher.UIThread.Post(() => SyncCoerced(property));
        }

        return coerced;
    }

    private void SyncCoerced(StyledProperty<double> property)
    {
        BindingOperations.GetBindingExpressionBase(this, property)?.UpdateSource();

        if ((property == StartProperty ? startBox : endBox) is { } box)
        {
            box.Value = GetValue(property);
        }
    }
}
