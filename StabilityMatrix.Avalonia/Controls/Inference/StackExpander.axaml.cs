using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[RegisterTransient<StackExpander>]
public class StackExpander : TemplatedControl
{
    public static readonly StyledProperty<bool> IsExpandedProperty = Expander
        .IsExpandedProperty
        .AddOwner<StackExpander>();

    public static readonly StyledProperty<ExpandDirection> ExpandDirectionProperty = Expander
        .ExpandDirectionProperty
        .AddOwner<StackExpander>();

    public static readonly StyledProperty<int> SpacingProperty = AvaloniaProperty.Register<StackCard, int>(
        "Spacing",
        8
    );

    public ExpandDirection ExpandDirection
    {
        get => GetValue(ExpandDirectionProperty);
        set => SetValue(ExpandDirectionProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public int Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}
