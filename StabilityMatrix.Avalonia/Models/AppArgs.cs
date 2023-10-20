namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Command line arguments passed to the application.
/// </summary>
public class AppArgs
{
    /// <summary>
    /// Whether to use the exception dialog while debugger is attached.
    /// When no debugger is attached, the exception dialog is always used.
    /// </summary>
    public bool DebugExceptionDialog { get; set; }

    /// <summary>
    /// Whether to use Sentry when a debugger is attached.
    /// </summary>
    public bool DebugSentry { get; set; }

    /// <summary>
    /// Whether to force show the one-click install dialog.
    /// </summary>
    public bool DebugOneClickInstall { get; set; }

    /// <summary>
    /// Whether to disable Sentry.
    /// </summary>
    public bool NoSentry { get; set; }

    /// <summary>
    /// Whether to disable window chrome effects
    /// </summary>
    public bool NoWindowChromeEffects { get; set; }

    /// <summary>
    /// Flag to indicate if we should reset the saved window position back to (O,0)
    /// </summary>
    public bool ResetWindowPosition { get; set; }

    /// <summary>
    /// Flag for disabling hardware acceleration / GPU rendering
    /// </summary>
    public bool DisableGpuRendering { get; set; }
}
