namespace StabilityMatrix.Core.Models.Settings;

/// <summary>
/// What the quick-hide hotkey does when pressed.
/// </summary>
public enum QuickHideMode
{
    /// <summary>Instantly minimize the main window.</summary>
    Minimize,

    /// <summary>Exit the app immediately, skipping confirmation prompts.</summary>
    Exit,
}
