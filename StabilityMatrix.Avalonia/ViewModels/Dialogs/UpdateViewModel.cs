using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Semver;
using StabilityMatrix.Avalonia.Languages;
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

    [ObservableProperty]
    private string? currentVersionText;

    [ObservableProperty]
    private string? newVersionText;

    [GeneratedRegex(
        @"(##\s*(v[0-9]+\.[0-9]+\.[0-9]+)((?:\n|.)+?))(?=(##\s*v[0-9]+\.[0-9]+\.[0-9]+)|\z)"
    )]
    private static partial Regex RegexChangelog();

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
        var pattern = RegexChangelog();

        var results = pattern
            .Matches(markdown)
            .Select(
                m =>
                    new
                    {
                        Block = m.Groups[1].Value.Trim(),
                        Version = m.Groups[2].Value.Trim(),
                        Content = m.Groups[3].Value.Trim()
                    }
            )
            .ToList();

        // Join all blocks until and excluding the current version
        // If we're on a pre-release, include the current release

        var currentVersionBlock = results.FindIndex(
            x => x.Version == $"v{currentVersion.WithoutPrereleaseOrMetadata()}"
        );

        if (currentVersionBlock == -1)
        {
            return null;
        }

        var blocks = results
            .Take(currentVersionBlock + (currentVersion.IsPrerelease ? 1 : 0))
            .Select(x => x.Block)
            .ToList();

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
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
                    FormatChangelog(changelog, Compat.AppVersion)
                    ?? "## Unable to format release notes";
            }
        }
        else
        {
            ReleaseNotes = "## Unable to load release notes";
        }
    }

    partial void OnUpdateInfoChanged(UpdateInfo? value)
    {
        CurrentVersionText = $"v{Compat.AppVersion}";
        NewVersionText = $"v{value?.Version}";
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
        UpdateText = string.Format(
            Resources.TextTemplate_UpdatingPackage,
            Resources.Label_StabilityMatrix
        );
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
