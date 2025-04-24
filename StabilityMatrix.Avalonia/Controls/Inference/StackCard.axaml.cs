using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls.Primitives;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[RegisterTransient<StackCard>]
public class StackCard : TemplatedControlBase
{
    public static readonly StyledProperty<int> SpacingProperty = AvaloniaProperty.Register<StackCard, int>(
        "Spacing",
        4
    );

    public int Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}
