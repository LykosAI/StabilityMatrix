using Avalonia;
using Avalonia.Controls.Primitives;

namespace StabilityMatrix.Avalonia.Controls;

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
