using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Compatibility layer for checks and file paths on different platforms.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class Compat
{
    private const string AppName = "StabilityMatrix";
    
    // OS Platform
    public static PlatformKind Platform { get; }
    public static bool IsWindows => Platform.HasFlag(PlatformKind.Windows);
    public static bool IsLinux => Platform.HasFlag(PlatformKind.Linux);
    public static bool IsMacOS => Platform.HasFlag(PlatformKind.MacOS);
    public static bool IsUnix => Platform.HasFlag(PlatformKind.Unix);
    
    // Paths
    
    /// <summary>
    /// AppData directory path. On Windows this is %AppData%, on Linux and MacOS this is ~/.config
    /// </summary>
    public static DirectoryPath AppData { get; }
    
    /// <summary>
    /// AppData + AppName (e.g. %AppData%\StabilityMatrix)
    /// </summary>
    public static DirectoryPath AppDataHome { get; }

    /// <summary>
    /// Current directory the app is in.
    /// </summary>
    public static DirectoryPath AppCurrentDir { get; }
    
    static Compat()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Platform = PlatformKind.Windows;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Platform = PlatformKind.MacOS | PlatformKind.Unix;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Platform = PlatformKind.Linux | PlatformKind.Unix;
        }
        
        AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppDataHome = AppData + AppName;
        
        AppCurrentDir = Platform switch 
        {
            PlatformKind.Windows => AppContext.BaseDirectory,
            PlatformKind.Linux => Environment.GetEnvironmentVariable("APPIMAGE") ??
                                  Environment.CurrentDirectory,
            _ => throw new PlatformNotSupportedException($"{Platform}")
        };
    }
}
