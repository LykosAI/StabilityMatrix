using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class StackCard : TemplatedControl
{
    public static readonly StyledProperty<int> SpacingProperty = AvaloniaProperty.Register<StackCard, int>(
        "Spacing", 8);

    public int Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}
