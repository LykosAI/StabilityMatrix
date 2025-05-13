using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Avalonia.Platform;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;

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
    public static Uri NoImage { get; } = new("avares://StabilityMatrix.Avalonia/Assets/noimage.png");

    public static AvaloniaResource LicensesJson =>
        new("avares://StabilityMatrix.Avalonia/Assets/licenses.json");

    public static AvaloniaResource ImagePromptLanguageJson =>
        new("avares://StabilityMatrix.Avalonia/Assets/ImagePrompt.tmLanguage.json");

    public static AvaloniaResource ThemeMatrixDarkJson =>
        new("avares://StabilityMatrix.Avalonia/Assets/ThemeMatrixDark.json");

    public static AvaloniaResource HfPackagesJson =>
        new("avares://StabilityMatrix.Avalonia/Assets/hf-packages.json");

    public static AvaloniaResource MarkdownCss =>
        new("avares://StabilityMatrix.Avalonia/Assets/markdown.css");

    private const UnixFileMode Unix755 =
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
                new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/linux-x64/7zzs", Unix755)
            ),
            (
                PlatformKind.MacOS | PlatformKind.Arm,
                new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/macos-arm64/7zz", Unix755)
            )
        );

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static AvaloniaResource SevenZipLicense =>
        Compat.Switch(
            (
                PlatformKind.Windows,
                new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/win-x64/7za - LICENSE.txt")
            ),
            (
                PlatformKind.Linux | PlatformKind.X64,
                new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/linux-x64/7zzs - LICENSE.txt")
            ),
            (
                PlatformKind.MacOS | PlatformKind.Arm,
                new AvaloniaResource("avares://StabilityMatrix.Avalonia/Assets/macos-arm64/7zz - LICENSE.txt")
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
                new RemoteResource
                {
                    Url = new Uri("https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip"),
                    HashSha256 = "608619f8619075629c9c69f361352a0da6ed7e62f83a0e19c63e0ea32eb7629d"
                }
            ),
            (
                PlatformKind.Linux | PlatformKind.X64,
                new RemoteResource
                {
                    Url = new Uri(
                        "https://github.com/indygreg/python-build-standalone/releases/download/20230507/cpython-3.10.11+20230507-x86_64-unknown-linux-gnu-install_only.tar.gz"
                    ),
                    HashSha256 = "c5bcaac91bc80bfc29cf510669ecad12d506035ecb3ad85ef213416d54aecd79"
                }
            ),
            (
                PlatformKind.MacOS | PlatformKind.Arm,
                new RemoteResource
                {
                    // Requires our distribution with signed dylib for gatekeeper
                    Url = new Uri("https://cdn.lykos.ai/cpython-3.10.11-macos-arm64.zip"),
                    HashSha256 = "83c00486e0af9c460604a425e519d58e4b9604fbe7a4448efda0f648f86fb6e3"
                }
            )
        );

    public static IReadOnlyList<RemoteResource> DefaultCompletionTags { get; } =
        new[]
        {
            new RemoteResource
            {
                Url = new Uri("https://cdn.lykos.ai/tags/danbooru.csv"),
                HashSha256 = "b84a879f1d9c47bf4758d66542598faa565b1571122ae12e7b145da8e7a4c1c6"
            },
            new RemoteResource
            {
                Url = new Uri("https://cdn.lykos.ai/tags/e621.csv"),
                HashSha256 = "ef7ea148ad865ad936d0c1ee57f0f83de723b43056c70b07fd67dbdbb89cae35"
            },
            new RemoteResource
            {
                Url = new Uri("https://cdn.lykos.ai/tags/danbooru_e621_merged.csv"),
                HashSha256 = "ac405ebce8b0caae363a7ef91f89beb4b8f60a7e218deb5078833686da6d497d"
            }
        };

    public static Uri DiscordServerUrl { get; } = new("https://discord.com/invite/TUrgfECxHz");

    public static Uri LykosUrl { get; } = new("https://lykos.ai");

    public static Uri PatreonUrl { get; } = new("https://patreon.com/StabilityMatrix");

    public static Uri CivitAIUrl { get; } = new("https://civitai.com");

    public static Uri LykosForgotPasswordUrl { get; } = new("https://lykos.ai/forgot-password");

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
