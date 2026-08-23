using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// A game-style hotkey picker: focus the box and press the desired key combination to set it.
/// Shows a live preview while modifiers are held; Escape cancels a pending capture. Produces
/// gesture strings like "Ctrl+Shift+K", where Ctrl represents the platform command modifier
/// (resolved to Cmd on macOS by <see cref="MarkupExtensions.PlatformGestureExtension.Parse"/>).
/// Plain typing keys are rejected unless combined with a modifier (F-keys excepted), so an
/// app-wide hotkey can't collide with normal text input.
/// </summary>
public class KeyGestureBox : TextBox
{
    public static readonly StyledProperty<string?> GestureProperty = AvaloniaProperty.Register<
        KeyGestureBox,
        string?
    >(nameof(Gesture), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Gesture string, e.g. "Ctrl+Shift+K".</summary>
    public string? Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    public KeyGestureBox()
    {
        IsReadOnly = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GestureProperty)
        {
            Text = change.GetNewValue<string?>();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Let Tab / Shift+Tab keep moving focus
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;

        if (e.Key is Key.Escape)
        {
            Text = Gesture;
            return;
        }

        if (ModifierFromKey(e.Key) is { } heldModifier)
        {
            // Live preview while only modifiers are held
            Text = FormatModifiers(e.KeyModifiers | heldModifier) + "...";
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key is not (>= Key.F1 and <= Key.F24))
        {
            Text = Gesture;
            return;
        }

        Gesture = FormatModifiers(e.KeyModifiers) + e.Key;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        // Drop the live modifier preview when nothing was committed
        if (Text != Gesture)
        {
            Text = Gesture;
        }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        Text = Gesture;
    }

    private static KeyModifiers? ModifierFromKey(Key key) =>
        key switch
        {
            Key.LeftCtrl or Key.RightCtrl => KeyModifiers.Control,
            Key.LeftShift or Key.RightShift => KeyModifiers.Shift,
            Key.LeftAlt or Key.RightAlt => KeyModifiers.Alt,
            Key.LWin or Key.RWin => KeyModifiers.Meta,
            _ => null,
        };

    private static string FormatModifiers(KeyModifiers modifiers)
    {
        var result = string.Empty;

        // On macOS the physical ⌘ key (Meta) is stored as Ctrl so it round-trips through
        // PlatformGestureExtension.Parse; elsewhere Meta is the Windows/Super key.
        if (
            modifiers.HasFlag(KeyModifiers.Control)
            || (Compat.IsMacOS && modifiers.HasFlag(KeyModifiers.Meta))
        )
        {
            result += "Ctrl+";
        }

        if (!Compat.IsMacOS && modifiers.HasFlag(KeyModifiers.Meta))
        {
            result += "Win+";
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result += "Alt+";
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result += "Shift+";
        }

        return result;
    }
}
