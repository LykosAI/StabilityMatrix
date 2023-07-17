using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
    
    [SupportedOSPlatformGuard("windows")]
    public static bool IsWindows => Platform.HasFlag(PlatformKind.Windows);
    
    [SupportedOSPlatformGuard("linux")]
    public static bool IsLinux => Platform.HasFlag(PlatformKind.Linux);
    
    [SupportedOSPlatformGuard("macos")]
    public static bool IsMacOS => Platform.HasFlag(PlatformKind.MacOS);
    public static bool IsUnix => Platform.HasFlag(PlatformKind.Unix);

    public static bool IsArm => Platform.HasFlag(PlatformKind.Arm);
    public static bool IsX64 => Platform.HasFlag(PlatformKind.X64);
    
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
    
    // File extensions
    /// <summary>
    /// Platform-specific executable extension.
    /// ".exe" on Windows, Empty string on Linux and MacOS.
    /// </summary>
    public static string ExeExtension { get; }

    /// <summary>
    /// Platform-specific dynamic library extension.
    /// ".dll" on Windows, ".dylib" on MacOS, ".so" on Linux.
    /// </summary>
    public static string DllExtension { get; }
    
    static Compat()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Platform = PlatformKind.Windows;
            AppCurrentDir = AppContext.BaseDirectory;
            ExeExtension = ".exe";
            DllExtension = ".dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Platform = PlatformKind.MacOS | PlatformKind.Unix;
            AppCurrentDir = AppContext.BaseDirectory; // TODO: check this
            ExeExtension = "";
            DllExtension = ".dylib";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Platform = PlatformKind.Linux | PlatformKind.Unix;
            // We need to get application path using `$APPIMAGE`, then get the directory name
            var appPath = Environment.GetEnvironmentVariable("APPIMAGE") ??
                          throw new Exception("Could not find application path");
            AppCurrentDir = Path.GetDirectoryName(appPath) ??
                            throw new Exception("Could not find application directory");
            ExeExtension = "";
            DllExtension = ".so";
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
        
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm)
        {
            Platform |= PlatformKind.Arm;
        }
        else
        {
            Platform |= PlatformKind.X64;
        }
        
        AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppDataHome = AppData + AppName;
    }
    
    /// <summary>
    /// Get the current application executable name.
    /// </summary>
    public static string GetExecutableName()
    {
        using var process = Process.GetCurrentProcess();
        
        var fullPath = process.MainModule?.ModuleName;

        if (string.IsNullOrEmpty(fullPath)) throw new Exception("Could not find executable name");
        
        return Path.GetFileName(fullPath);
    }
}
