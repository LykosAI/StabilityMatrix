using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace StabilityMatrix.Avalonia.Controls;

public class Card : ContentControl
{
    protected override Type StyleKeyOverride => typeof(Card);

    // ReSharper disable MemberCanBePrivate.Global
    public static readonly StyledProperty<bool> IsCardVisualsEnabledProperty =
        AvaloniaProperty.Register<Card, bool>("IsCardVisualsEnabled", true);

    /// <summary>
    /// Whether to show card visuals.
    /// When false, the card will have a padding of 0 and be transparent.
    /// </summary>
    public bool IsCardVisualsEnabled
    {
        get => GetValue(IsCardVisualsEnabledProperty);
        set => SetValue(IsCardVisualsEnabledProperty, value);
    }

    // ReSharper restore MemberCanBePrivate.Global

    public Card()
    {
        MinHeight = 8;
        MinWidth = 8;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // When IsCardVisualsEnabled is false, add the disabled pseudo class
        if (change.Property == IsCardVisualsEnabledProperty)
        {
            PseudoClasses.Set("disabled", !change.GetNewValue<bool>());
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        PseudoClasses.Set("disabled", !IsCardVisualsEnabled);
    }
}
