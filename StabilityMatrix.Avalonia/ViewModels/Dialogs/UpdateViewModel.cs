using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(UpdateDialog))]
[ManagedService]
[RegisterSingleton<UpdateViewModel>]
public partial class UpdateViewModel : ContentDialogViewModelBase
{
    private readonly ILogger<UpdateViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IUpdateHelper updateHelper;
    private readonly IChangelogService changelogService;

    private bool isLoaded;

    [ObservableProperty]
    private bool isUpdateAvailable;

    [ObservableProperty]
    private UpdateInfo? updateInfo;

    [ObservableProperty]
    private string? releaseNotes;

    [ObservableProperty]
    private string? updateText;

    [ObservableProperty]
    private int progressValue;

    [ObservableProperty]
    private bool isProgressIndeterminate;

    [ObservableProperty]
    private bool showProgressBar;

    [ObservableProperty]
    private string? currentVersionText;

    [ObservableProperty]
    private string? newVersionText;

    public UpdateViewModel(
        ILogger<UpdateViewModel> logger,
        ISettingsManager settingsManager,
        IUpdateHelper updateHelper,
        IChangelogService changelogService
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.updateHelper = updateHelper;
        this.changelogService = changelogService;

        EventManager.Instance.UpdateAvailable += (_, info) =>
        {
            IsUpdateAvailable = true;
            UpdateInfo = info;
        };
    }

    public async Task Preload()
    {
        if (UpdateInfo is null)
            return;

        ReleaseNotes = await changelogService.GetReleaseNotes(UpdateInfo.Changelog.ToString());
    }

    partial void OnUpdateInfoChanged(UpdateInfo? value)
    {
        CurrentVersionText = $"v{Compat.AppVersion.ToDisplayString()}";
        NewVersionText = $"v{value?.Version.ToDisplayString()}";
    }

    public override async Task OnLoadedAsync()
    {
        if (!isLoaded)
        {
            await Preload();
        }
    }

    /// <inheritdoc />
    public override void OnUnloaded()
    {
        base.OnUnloaded();
        isLoaded = false;
    }

    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (UpdateInfo == null)
        {
            return;
        }

        ShowProgressBar = true;
        IsProgressIndeterminate = true;
        UpdateText = string.Format(Resources.TextTemplate_UpdatingPackage, Resources.Label_StabilityMatrix);

        try
        {
            await updateHelper.DownloadUpdate(
                UpdateInfo,
                new Progress<ProgressReport>(report =>
                {
                    ProgressValue = Convert.ToInt32(report.Percentage);
                    IsProgressIndeterminate = report.IsIndeterminate;
                })
            );
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to download update");

            var dialog = DialogHelper.CreateMarkdownDialog(
                $"{e.GetType().Name}: {e.Message}",
                Resources.Label_UnexpectedErrorOccurred
            );

            await dialog.ShowAsync();
            return;
        }

        // On unix, we need to set the executable bit
        if (Compat.IsUnix)
        {
            File.SetUnixFileMode(
                UpdateHelper.ExecutablePath.FullPath,
                // 0755
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute
            );
        }

        // Handle Linux AppImage update
        if (Compat.IsLinux && Environment.GetEnvironmentVariable("APPIMAGE") is { } appImage)
        {
            try
            {
                var updateScriptPath = UpdateHelper.UpdateFolder.JoinFile("update_script.sh").FullPath;
                var newAppImage = UpdateHelper.ExecutablePath.FullPath;

                var scriptContent = """
#!/bin/bash
set -e

# Wait for the process to exit
while kill -0 "$PID" 2>/dev/null; do
    sleep 0.5
done

# Move the new AppImage over the old one
mv -f "$NEW_APPIMAGE" "$OLD_APPIMAGE"
chmod +x "$OLD_APPIMAGE"

# Launch the new AppImage detached
"$OLD_APPIMAGE" > /dev/null 2>&1 &
disown
""";
                await File.WriteAllTextAsync(updateScriptPath, scriptContent);

                File.SetUnixFileMode(
                    updateScriptPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                );

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/env",
                    Arguments = $"bash \"{updateScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                startInfo.EnvironmentVariables["PID"] = Environment.ProcessId.ToString();
                startInfo.EnvironmentVariables["NEW_APPIMAGE"] = newAppImage;
                startInfo.EnvironmentVariables["OLD_APPIMAGE"] = appImage;

                System.Diagnostics.Process.Start(startInfo);

                App.Shutdown();
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute AppImage update script");

                var dialog = DialogHelper.CreateMarkdownDialog(
                    "AppImage update script failed. \nCould not replace old AppImage with new version. Please check directory permissions. \nFalling back to standard update process. User intervention required: \nAfter program closes, \nplease move the new AppImage extracted in the '.StabilityMatrixUpdate' hidden directory to the old AppImage overwriting it. \n\nClose this dialog to continue with standard update process.",
                    Resources.Label_UnexpectedErrorOccurred
                );

                await dialog.ShowAsync();
            }
        }

        // Set current version for update messages
        settingsManager.Transaction(
            s => s.UpdatingFromVersion = Compat.AppVersion,
            ignoreMissingLibraryDir: true
        );

        UpdateText = "Getting a few things ready...";
        await using (new MinimumDelay(500, 1000))
        {
            await Task.Run(() =>
            {
                var args = new[] { "--wait-for-exit-pid", $"{Environment.ProcessId}" };

                if (Program.Args.NoSentry)
                {
                    args = args.Append("--no-sentry").ToArray();
                }

                ProcessRunner.StartApp(UpdateHelper.ExecutablePath.FullPath, args);
            });
        }

        UpdateText = "Update complete. Restarting Stability Matrix in 3 seconds...";
        await Task.Delay(1000);
        UpdateText = "Update complete. Restarting Stability Matrix in 2 seconds...";
        await Task.Delay(1000);
        UpdateText = "Update complete. Restarting Stability Matrix in 1 second...";
        await Task.Delay(1000);
        UpdateText = "Update complete. Restarting Stability Matrix...";

        App.Shutdown();
    }
}
