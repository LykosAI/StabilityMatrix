using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace StabilityMatrix.Avalonia.Behaviors;

/// <summary>
/// Behavior that sets tooltip to null if the DisableOn condition is true.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class ConditionalToolTipBehavior : Behavior<Control>
{
    public static readonly StyledProperty<bool> DisableOnProperty = AvaloniaProperty.Register<
        ConditionalToolTipBehavior,
        bool
    >("DisableOn");

    public bool DisableOn
    {
        get => GetValue(DisableOnProperty);
        set => SetValue(DisableOnProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (DisableOn)
        {
            ToolTip.SetTip(AssociatedObject!, null);
        }
    }
}
