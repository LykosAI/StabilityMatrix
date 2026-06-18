using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using NLog;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia.Helpers;

/// <summary>
/// Handles Linux desktop integration, including creating .desktop files.
/// Only relevant for AppImage runs - other Linux installs (deb/rpm/flatpak/AUR)
/// ship their own package-managed .desktop entries which we must not touch.
/// </summary>
public static class LinuxDesktopIntegration
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string DesktopFileTemplate = """
[Desktop Entry]
Name=Stability Matrix
Exec="{0}" %u
Type=Application
NoDisplay=false
Categories=Utility
Icon={1}
StartupWMClass=stabilitymatrix
""";

    /// <summary>
    /// Gets the current desktop environment name
    /// </summary>
    /// <returns>The name of the current desktop environment (e.g., "KDE", "GNOME", "XFCE") or null if not detected</returns>
    [SupportedOSPlatform("linux")]
    private static string? GetCurrentDesktopEnvironment()
    {
        try
        {
            // First check XDG_CURRENT_DESKTOP
            var xdgCurrentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
            if (!string.IsNullOrEmpty(xdgCurrentDesktop))
            {
                // XDG_CURRENT_DESKTOP can contain multiple values separated by colon
                var desktops = xdgCurrentDesktop.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (desktops.Length > 0)
                {
                    return desktops[0].ToUpperInvariant();
                }
            }

            // Fallback to DESKTOP_SESSION
            var desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
            if (!string.IsNullOrEmpty(desktopSession))
            {
                return desktopSession.ToUpperInvariant();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a correct .desktop entry (and extracts the icon) for AppImage runs so the app
    /// shows up in the application launcher. No-op when not running as an AppImage, since other
    /// install types manage their own desktop entries.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static void CreateDesktopFile()
    {
        if (!Compat.IsLinux)
        {
            return;
        }

        // Only self-integrate when running as an AppImage. Other installs (deb/rpm/flatpak/AUR)
        // have package-managed .desktop files, and Compat.AppCurrentPath throws off-AppImage.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")))
        {
            return;
        }

        try
        {
            var desktopFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local/share/applications/stabilitymatrix-app.desktop"
            );

            var iconPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local/share/icons/hicolor/256x256/apps/stabilitymatrix.png"
            );

            // Ensure directories exist
            Directory.CreateDirectory(Path.GetDirectoryName(desktopFilePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);

            // Extract icon (must be a real PNG - launchers can't render an .ico)
            using (var iconStream = Assets.AppIconPng.Open())
            using (var iconFileStream = File.Create(iconPath))
            {
                iconStream.CopyTo(iconFileStream);
            }

            // Create desktop file with additional desktop environment specific entries
            var desktopFileBuilder = new StringBuilder(
                string.Format(DesktopFileTemplate, Compat.AppCurrentPath.FullPath, iconPath)
            );

            // Add desktop environment specific entries
            var desktopEnv = GetCurrentDesktopEnvironment();
            if (!string.IsNullOrEmpty(desktopEnv))
            {
                switch (desktopEnv)
                {
                    case "KDE":
                        desktopFileBuilder.AppendLine("X-KDE-StartupNotify=true");
                        break;
                    case "GNOME":
                        desktopFileBuilder.AppendLine("X-GNOME-UsesNotifications=true");
                        break;
                    case "XFCE":
                        desktopFileBuilder.AppendLine("X-XFCE-StartupNotify=true");
                        break;
                }
            }

            File.WriteAllText(desktopFilePath, desktopFileBuilder.ToString(), Encoding.UTF8);

            // Make executable
            var unixFileMode =
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute;

            File.SetUnixFileMode(desktopFilePath, unixFileMode);

            Logger.Info("Created Linux desktop entry at {DesktopFilePath}", desktopFilePath);
        }
        catch (Exception e)
        {
            // Desktop integration is best-effort; never let it block startup
            Logger.Warn(e, "Failed to create Linux desktop entry");
        }
    }
}
