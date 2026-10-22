using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// A labelled start/end fraction of sampling steps (0 to 1), edited with two percent boxes and a range slider.
/// <see cref="Start"/> never ends up past <see cref="End"/>, and the boxes stay within 0 to 1.
/// </summary>
public class StepRangeEditor : TemplatedControl
{
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
    >(nameof(Start), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Fraction of steps where the range starts, at most <see cref="End"/>.</summary>
    public double Start
    {
        get => GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    public static readonly StyledProperty<double> EndProperty = AvaloniaProperty.Register<
        StepRangeEditor,
        double
    >(nameof(End), 1d, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Fraction of steps where the range ends, at least <see cref="Start"/>.</summary>
    public double End
    {
        get => GetValue(EndProperty);
        set => SetValue(EndProperty, value);
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
}
