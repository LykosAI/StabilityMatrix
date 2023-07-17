using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Platform;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia;

internal static class Assets
{
    /// <summary>
    /// Fixed image for models with no images.
    /// </summary>
    public static Uri NoImage { get; } =
        new("avares://StabilityMatrix.Avalonia/Assets/noimage.png");

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    public static AvaloniaResource SevenZipExecutable => Compat.Switch(
        (PlatformKind.Windows,
            new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/win-x64/7za.exe")),
        (PlatformKind.Linux | PlatformKind.X64,
            new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/linux-x64/7zzs",
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)));
    
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    public static AvaloniaResource SevenZipLicense => Compat.Switch(
        (PlatformKind.Windows, new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/win-x64/7za - LICENSE.txt")),
        (PlatformKind.Linux | PlatformKind.X64, new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/linux-x64/7zzs - LICENSE.txt")));
    
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static (Uri url, string hashSha256) PythonDownloadUrl
    {
        get
        {
            if (Compat.IsWindows)
            {
                return (new Uri("https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip"), 
                    "608619f8619075629c9c69f361352a0da6ed7e62f83a0e19c63e0ea32eb7629d");
            }
            if (Compat.Platform.HasFlag(PlatformKind.Linux | PlatformKind.X64))
            {
                return (new Uri("https://github.com/indygreg/python-build-standalone/releases/download/" +
                               "20230507/cpython-3.10.11+20230507-x86_64-unknown-linux-gnu-install_only.tar.gz"),
                        "c5bcaac91bc80bfc29cf510669ecad12d506035ecb3ad85ef213416d54aecd79");
            }
            if (Compat.Platform.HasFlag(PlatformKind.MacOS | PlatformKind.Arm))
            {
                return (new Uri("https://github.com/indygreg/python-build-standalone/releases/download/" +
                                "20230507/cpython-3.10.11+20230507-aarch64-apple-darwin-install_only.tar.gz"),
                    "8348bc3c2311f94ec63751fb71bd0108174be1c4def002773cf519ee1506f96f");
            }
            throw new PlatformNotSupportedException();
        }
    }
    
    
    public static Uri DiscordServerUrl { get; } =
        new("https://discord.com/invite/TUrgfECxHz"); 
    
    public static Uri PatreonUrl { get; } =
        new("https://patreon.com/StabilityMatrix"); 
    
    
    /// <summary>
    /// Extracts an asset URI to a target directory.
    /// </summary>
    public static async Task ExtractAsset(Uri assetUri, string targetDirectory, bool overwrite = true)
    {
        var assetName = Path.GetFileName(assetUri.ToString());
        var targetPath = Path.Combine(targetDirectory, assetName);
        if (File.Exists(targetPath) && !overwrite)
        {
            return;
        }
        var stream = AssetLoader.Open(assetUri);
        await using var fileStream = File.Create(targetPath);
        await stream.CopyToAsync(fileStream);
    }
}
