using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Avalonia.Platform;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Avalonia;

internal static class Assets
{
    public static AvaloniaResource AppIcon { get; } =
        new("avares://StabilityMatrix.Avalonia/Assets/Icon.ico");

    public static AvaloniaResource AppIconPng { get; } =
        new("avares://StabilityMatrix.Avalonia/Assets/Icon.png");

    /// <summary>
    /// Fixed image for models with no images.
    /// </summary>
    public static Uri NoImage { get; } =
        new("avares://StabilityMatrix.Avalonia/Assets/noimage.png");

    public static AvaloniaResource LicensesJson =>
        new("avares://StabilityMatrix.Avalonia/Assets/licenses.json");

    private const UnixFileMode unix755 =
        UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead
        | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead
        | UnixFileMode.OtherExecute;

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static AvaloniaResource SevenZipExecutable =>
        Compat.Switch(
            (
                PlatformKind.Windows,
                new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/win-x64/7za.exe")
            ),
            (
                PlatformKind.Linux | PlatformKind.X64,
                new AvaloniaResource(
                    "avares://StabilityMatrix.Avalonia/Assets/linux-x64/7zzs",
                    unix755
                )
            ),
            (
                PlatformKind.MacOS | PlatformKind.Arm,
                new AvaloniaResource(
                    "avares://StabilityMatrix.Avalonia/Assets/macos-arm64/7zz",
                    unix755
                )
            )
        );

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static AvaloniaResource SevenZipLicense =>
        Compat.Switch(
            (
                PlatformKind.Windows,
                new AvaloniaResource(
                    "avares://StabilityMatrix.Avalonia/Assets/win-x64/7za - LICENSE.txt"
                )
            ),
            (
                PlatformKind.Linux | PlatformKind.X64,
                new AvaloniaResource(
                    "avares://StabilityMatrix.Avalonia/Assets/linux-x64/7zzs - LICENSE.txt"
                )
            ),
            (
                PlatformKind.MacOS | PlatformKind.Arm,
                new AvaloniaResource(
                    "avares://StabilityMatrix.Avalonia/Assets/macos-arm64/7zz - LICENSE.txt"
                )
            )
        );

    public static AvaloniaResource PyScriptSiteCustomize =>
        new("avares://StabilityMatrix.Avalonia/Assets/sitecustomize.py");

    [SupportedOSPlatform("windows")]
    public static AvaloniaResource PyScriptGetPip =>
        new("avares://StabilityMatrix.Avalonia/Assets/win-x64/get-pip.pyc");

    [SupportedOSPlatform("windows")]
    public static IEnumerable<(AvaloniaResource resource, string relativePath)> PyModuleVenv =>
        FindAssets("win-x64/venv/");

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static RemoteResource PythonDownloadUrl =>
        Compat.Switch(
            (
                PlatformKind.Windows | PlatformKind.X64,
                new RemoteResource(
                    new Uri(
                        "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip"
                    ),
                    "608619f8619075629c9c69f361352a0da6ed7e62f83a0e19c63e0ea32eb7629d"
                )
            ),
            (
                PlatformKind.Linux | PlatformKind.X64,
                new RemoteResource(
                    new Uri(
                        "https://github.com/indygreg/python-build-standalone/releases/download/20230507/cpython-3.10.11+20230507-x86_64-unknown-linux-gnu-install_only.tar.gz"
                    ),
                    "c5bcaac91bc80bfc29cf510669ecad12d506035ecb3ad85ef213416d54aecd79"
                )
            ),
            (
                PlatformKind.MacOS | PlatformKind.Arm,
                new RemoteResource(
                    new Uri(
                        "https://github.com/indygreg/python-build-standalone/releases/download/20230507/cpython-3.10.11+20230507-aarch64-apple-darwin-install_only.tar.gz"
                    ),
                    "8348bc3c2311f94ec63751fb71bd0108174be1c4def002773cf519ee1506f96f"
                )
            )
        );

    public static Uri DiscordServerUrl { get; } = new("https://discord.com/invite/TUrgfECxHz");

    public static Uri PatreonUrl { get; } = new("https://patreon.com/StabilityMatrix");

    /// <summary>
    /// Yield AvaloniaResources given a relative directory path within the 'Assets' folder.
    /// </summary>
    public static IEnumerable<(AvaloniaResource resource, string relativePath)> FindAssets(
        string relativeAssetPath
    )
    {
        var baseUri = new Uri("avares://StabilityMatrix.Avalonia/Assets/");
        var targetUri = new Uri(baseUri, relativeAssetPath);
        var files = AssetLoader.GetAssets(targetUri, null);
        foreach (var file in files)
        {
            yield return (new AvaloniaResource(file), targetUri.MakeRelativeUri(file).ToString());
        }
    }
}
