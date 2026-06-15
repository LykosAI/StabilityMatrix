using Avalonia.Input;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.MarkupExtensions;

/// <summary>
/// Provides a <see cref="KeyGesture"/> whose primary modifier follows the platform convention:
/// the <c>Ctrl</c> token resolves to <c>Cmd</c> (⌘ / Meta) on macOS and stays <c>Ctrl</c> elsewhere.
/// Usage: <c>Gesture="{markupExtensions:PlatformGesture Ctrl+S}"</c>
/// </summary>
public class PlatformGestureExtension : MarkupExtension
{
    /// <summary>
    /// The gesture string, e.g. <c>Ctrl+S</c> or <c>Ctrl+Shift+Tab</c>. This is the default
    /// constructor argument. The <c>Ctrl</c>/<c>Control</c> token is swapped for the platform
    /// command key on macOS.
    /// </summary>
    public string? Gesture { get; set; }

    public PlatformGestureExtension() { }

    public PlatformGestureExtension(string gesture)
    {
        Gesture = gesture;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Gesture))
        {
            throw new InvalidOperationException("PlatformGesture requires a gesture string.");
        }

        var gesture = Gesture;

        if (Compat.IsMacOS)
        {
            // KeyGesture.Parse maps the "Cmd" token to KeyModifiers.Meta (⌘).
            gesture = gesture
                .Replace("Ctrl", "Cmd", StringComparison.OrdinalIgnoreCase)
                .Replace("Control", "Cmd", StringComparison.OrdinalIgnoreCase);
        }

        return KeyGesture.Parse(gesture);
    }
}
