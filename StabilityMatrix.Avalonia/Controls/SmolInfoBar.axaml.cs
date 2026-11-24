using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Avalonia;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Compact, single-strip info/warning bar. The stock <c>ui:InfoBar</c>'s padding and stacked action
/// button eat too much vertical space inside the inference cards, so this is a slimmer drop-in: an icon,
/// a wrapping message, an optional action button and a dismiss button on a single row.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class SmolInfoBar : TemplatedControlBase
{
    public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<
        SmolInfoBar,
        string?
    >(nameof(Message));

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly StyledProperty<SmolInfoBarSeverity> SeverityProperty = AvaloniaProperty.Register<
        SmolInfoBar,
        SmolInfoBarSeverity
    >(nameof(Severity), SmolInfoBarSeverity.Informational);

    public SmolInfoBarSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<SmolInfoBar, bool>(
        nameof(IsOpen)
    );

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static readonly StyledProperty<string?> ActionTextProperty = AvaloniaProperty.Register<
        SmolInfoBar,
        string?
    >(nameof(ActionText));

    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ActionCommandProperty = AvaloniaProperty.Register<
        SmolInfoBar,
        ICommand?
    >(nameof(ActionCommand));

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public static readonly StyledProperty<object?> ActionToolTipProperty = AvaloniaProperty.Register<
        SmolInfoBar,
        object?
    >(nameof(ActionToolTip));

    public object? ActionToolTip
    {
        get => GetValue(ActionToolTipProperty);
        set => SetValue(ActionToolTipProperty, value);
    }

    public static readonly StyledProperty<bool> IsClosableProperty = AvaloniaProperty.Register<
        SmolInfoBar,
        bool
    >(nameof(IsClosable), true);

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<
        SmolInfoBar,
        ICommand?
    >(nameof(CloseCommand));

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
}

public enum SmolInfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}
