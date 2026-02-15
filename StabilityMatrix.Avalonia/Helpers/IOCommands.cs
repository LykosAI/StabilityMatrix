using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.Helpers;

public static class IOCommands
{
    public static RelayCommand<string?> OpenUrlCommand { get; } =
        new(
            url =>
            {
                if (string.IsNullOrWhiteSpace(url))
                    return;

                ProcessRunner.OpenUrl(url);
            },
            url => !string.IsNullOrWhiteSpace(url)
        );

    public static RelayCommand<Uri?> OpenUriCommand { get; } =
        new(
            url =>
            {
                if (url is null)
                    return;

                ProcessRunner.OpenUrl(url);
            },
            url => url is not null
        );

    public static AsyncRelayCommand<string?> OpenFileBrowserCommand { get; } =
        new(
            async path =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                await ProcessRunner.OpenFileBrowser(path);
            },
            path => !string.IsNullOrWhiteSpace(path)
        );

    public static AsyncRelayCommand<string?> OpenFolderBrowserCommand { get; } =
        new(
            async path =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                await ProcessRunner.OpenFolderBrowser(path);
            },
            path => !string.IsNullOrWhiteSpace(path)
        );
}
