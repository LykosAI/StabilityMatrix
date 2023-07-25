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
    /// Whether to disable Sentry.
    /// </summary>
    public bool NoSentry { get; set; }
    
    /// <summary>
    /// Whether to disable window chrome effects
    /// </summary>
    public bool NoWindowChromeEffects { get; set; }
}
