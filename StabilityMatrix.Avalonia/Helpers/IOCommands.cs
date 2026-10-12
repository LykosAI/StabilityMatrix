using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
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

                await OpenFileBrowserAsync(path);
            },
            path => !string.IsNullOrWhiteSpace(path)
        );

    public static AsyncRelayCommand<string?> OpenFolderBrowserCommand { get; } =
        new(
            async path =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                await OpenFolderBrowserAsync(path);
            },
            path => !string.IsNullOrWhiteSpace(path)
        );

    /// <summary>
    /// Opens the folder in the system file explorer, or shows a notification if it does not exist.
    /// </summary>
    public static async Task OpenFolderBrowserAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            ShowNotFound(Resources.Label_FolderNotFound, directoryPath);
            return;
        }

        await ProcessRunner.OpenFolderBrowser(directoryPath);
    }

    /// <summary>
    /// Opens the file within its folder in the system file explorer, or shows a notification if it does not exist.
    /// </summary>
    public static async Task OpenFileBrowserAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ShowNotFound(Resources.Label_FileNotFound, filePath);
            return;
        }

        await ProcessRunner.OpenFileBrowser(filePath);
    }

    private static void ShowNotFound(string title, string path)
    {
        App.Services.GetRequiredService<INotificationService>().Show(title, path, NotificationType.Warning);
    }
}
