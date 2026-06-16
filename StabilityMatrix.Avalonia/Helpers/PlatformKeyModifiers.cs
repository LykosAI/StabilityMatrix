using Avalonia.Input;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Platform-aware keyboard modifiers for code-behind shortcut handling.
/// </summary>
public static class PlatformKeyModifiers
{
    /// <summary>
    /// The primary command modifier for the current platform:
    /// <see cref="KeyModifiers.Meta"/> (⌘) on macOS, <see cref="KeyModifiers.Control"/> elsewhere.
    /// </summary>
    public static KeyModifiers CommandModifier => Compat.IsMacOS ? KeyModifiers.Meta : KeyModifiers.Control;
}
