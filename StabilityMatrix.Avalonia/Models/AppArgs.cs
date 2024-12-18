using CommandLine;

namespace StabilityMatrix.Avalonia.Models;

/// <summary>
/// Command line arguments passed to the application.
/// </summary>
public class AppArgs
{
    /// <summary>
    /// Whether to enable debug mode
    /// </summary>
    [Option("debug", HelpText = "Enable debug mode")]
    public bool DebugMode { get; set; }

    /// <summary>
    /// Whether to use the exception dialog while debugger is attached.
    /// When no debugger is attached, the exception dialog is always used.
    /// </summary>
    [Option("debug-exception-dialog", HelpText = "Use exception dialog while debugger is attached")]
    public bool DebugExceptionDialog { get; set; }

    /// <summary>
    /// Whether to use Sentry when a debugger is attached.
    /// </summary>
    [Option("debug-sentry", HelpText = "Use Sentry when debugger is attached")]
    public bool DebugSentry { get; set; }

    /// <summary>
    /// Whether to force show the one-click install dialog.
    /// </summary>
    [Option("debug-one-click-install", HelpText = "Force show the one-click install dialog")]
    public bool DebugOneClickInstall { get; set; }

    /// <summary>
    /// Whether to disable Sentry.
    /// </summary>
    [Option("no-sentry", HelpText = "Disable Sentry")]
    public bool NoSentry { get; set; }

    /// <summary>
    /// Whether to disable window chrome effects
    /// </summary>
    [Option("no-window-chrome-effects", HelpText = "Disable window chrome effects")]
    public bool NoWindowChromeEffects { get; set; }

    /// <summary>
    /// Flag to indicate if we should reset the saved window position back to (O,0)
    /// </summary>
    [Option("reset-window-position", HelpText = "Reset the saved window position back to (0,0)")]
    public bool ResetWindowPosition { get; set; }

    /// <summary>
    /// Flag to enable the splash screen on startup
    /// </summary>
    [Option("splash-screen", HelpText = "Enable the startup splash screen")]
    public bool IsSplashScreenEnabled { get; set; }

    /// <summary>
    /// Flag for disabling hardware acceleration / GPU rendering
    /// </summary>
    [Option("disable-gpu-rendering", HelpText = "Disable hardware acceleration / GPU rendering")]
    public bool DisableGpuRendering { get; set; }

    /// <summary>
    /// Flag to use OpenGL rendering
    /// </summary>
    [Option("opengl", HelpText = "Prefer OpenGL rendering")]
    public bool UseOpenGlRendering { get; set; }

    /// <summary>
    /// Flag to use Vulkan rendering
    /// </summary>
    [Option("vulkan", HelpText = "Prefer Vulkan rendering")]
    public bool UseVulkanRendering { get; set; }

    /// <summary>
    /// Override global app home directory
    /// Defaults to (%APPDATA%|~/.config)/StabilityMatrix
    /// </summary>
    [Option("home-dir", HelpText = "Override global app home directory")]
    public string? HomeDirectoryOverride { get; set; }

    /// <summary>
    /// Override data directory
    /// This takes precedence over relative portable directory and global directory
    /// </summary>
    [Option("data-dir", HelpText = "Override data directory")]
    public string? DataDirectoryOverride { get; set; }

    /// <summary>
    /// Launch an installed package on startup
    /// Can use package ID or name
    /// </summary>
    [Option("launch-package", HelpText = "Package ID or name to launch on startup")]
    public string? LaunchPackageName { get; set; }

    /// <summary>
    /// Custom Uri protocol handler
    /// This will send the Uri to the running instance of the app via IPC and exit
    /// </summary>
    [Option("uri", Hidden = true)]
    public string? Uri { get; set; }

    /// <summary>
    /// If provided, the app will wait for the process with this PID to exit
    /// before starting up. Mainly used by the updater.
    /// </summary>
    [Option("wait-for-exit-pid", Hidden = true)]
    public int? WaitForExitPid { get; set; }
}
