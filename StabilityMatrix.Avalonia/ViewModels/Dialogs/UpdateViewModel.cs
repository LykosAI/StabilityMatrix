using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Semver;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(UpdateDialog))]
public partial class UpdateViewModel : ContentDialogViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IUpdateHelper updateHelper;

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
    private bool showProgressBar;

    public UpdateViewModel(
        ISettingsManager settingsManager,
        IHttpClientFactory httpClientFactory,
        IUpdateHelper updateHelper
    )
    {
        this.settingsManager = settingsManager;
        this.httpClientFactory = httpClientFactory;
        this.updateHelper = updateHelper;

        EventManager.Instance.UpdateAvailable += (_, info) =>
        {
            IsUpdateAvailable = true;
            UpdateInfo = info;
        };
        updateHelper.StartCheckingForUpdates().SafeFireAndForget();
    }

    /// <summary>
    /// Formats changelog markdown including up to the current version
    /// </summary>
    internal static string? FormatChangelog(string markdown, SemVersion currentVersion)
    {
        var pattern = $@"(##[\s\S]+?)(?:## v{currentVersion.WithoutPrereleaseOrMetadata()})";

        var match = Regex.Match(markdown, pattern);

        return match.Success ? match.Groups[1].Value.TrimEnd() : null;
    }

    public async Task Preload()
    {
        if (UpdateInfo is null)
            return;

        using var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(UpdateInfo.ChangelogUrl);
        if (response.IsSuccessStatusCode)
        {
            var changelog = await response.Content.ReadAsStringAsync();

            // Formatting for new changelog format
            // https://keepachangelog.com/en/1.1.0/
            if (UpdateInfo.ChangelogUrl.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                ReleaseNotes =
                    FormatChangelog(changelog, UpdateInfo.Version)
                    ?? "## Unable to format release notes";
            }
        }
        else
        {
            ReleaseNotes = "## Unable to load release notes";
        }
    }

    public override async Task OnLoadedAsync()
    {
        if (UpdateInfo is null)
            return;

        UpdateText =
            $"Stability Matrix v{UpdateInfo.Version} is now available! You currently have v{Compat.AppVersion}. Would you like to update now?";

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
        UpdateText = $"Downloading update v{UpdateInfo.Version}...";
        await updateHelper.DownloadUpdate(
            UpdateInfo,
            new Progress<ProgressReport>(report =>
            {
                ProgressValue = Convert.ToInt32(report.Percentage);
            })
        );

        // On unix, we need to set the executable bit
        if (Compat.IsUnix)
        {
            File.SetUnixFileMode(
                UpdateHelper.ExecutablePath, // 0755
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute
            );
        }

        UpdateText = "Update complete. Restarting Stability Matrix in 3 seconds...";
        await Task.Delay(1000);
        UpdateText = "Update complete. Restarting Stability Matrix in 2 seconds...";
        await Task.Delay(1000);
        UpdateText = "Update complete. Restarting Stability Matrix in 1 second...";
        await Task.Delay(1000);

        Process.Start(UpdateHelper.ExecutablePath);
        App.Shutdown();
    }
}
