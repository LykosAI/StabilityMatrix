using Avalonia;
using Avalonia.Controls.Primitives;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// The generate button row at the bottom of an inference tab: a recessed pill holding the
/// Generate / Cancel button, flanked by optional hires-fix and current-seed buttons and the
/// add-to-queue button. Requires an
/// <see cref="ViewModels.Base.InferenceGenerationViewModelBase"/> DataContext.
/// </summary>
public class GenerateButtonBar : TemplatedControl
{
    public static readonly StyledProperty<bool> ShowHiresFixButtonProperty = AvaloniaProperty.Register<
        GenerateButtonBar,
        bool
    >(nameof(ShowHiresFixButton));

    /// <summary>
    /// Whether the hires-fix button is shown to the left of the generate button.
    /// </summary>
    public bool ShowHiresFixButton
    {
        get => GetValue(ShowHiresFixButtonProperty);
        set => SetValue(ShowHiresFixButtonProperty, value);
    }

    public static readonly StyledProperty<bool> ShowCurrentSeedButtonProperty = AvaloniaProperty.Register<
        GenerateButtonBar,
        bool
    >(nameof(ShowCurrentSeedButton));

    /// <summary>
    /// Whether the current-seed button is shown to the right of the generate button.
    /// </summary>
    public bool ShowCurrentSeedButton
    {
        get => GetValue(ShowCurrentSeedButtonProperty);
        set => SetValue(ShowCurrentSeedButtonProperty, value);
    }

    public static readonly StyledProperty<double> PillMaxWidthProperty = AvaloniaProperty.Register<
        GenerateButtonBar,
        double
    >(nameof(PillMaxWidth), 400);

    /// <summary>
    /// Width cap for the pill. It is a cap and not a fixed width, so the pill shrinks with the
    /// pane rather than overflowing it; bars with fewer flank buttons want a smaller one.
    /// </summary>
    public double PillMaxWidth
    {
        get => GetValue(PillMaxWidthProperty);
        set => SetValue(PillMaxWidthProperty, value);
    }
}
