using Avalonia;
using Avalonia.Controls.Primitives;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[Transient]
public class StackExpander : TemplatedControl
{
    public static readonly StyledProperty<int> SpacingProperty = AvaloniaProperty.Register<
        StackCard,
        int
    >("Spacing", 8);

    public int Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}
