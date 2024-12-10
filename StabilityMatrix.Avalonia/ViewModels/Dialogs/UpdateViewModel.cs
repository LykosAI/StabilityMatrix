using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Semver;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
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
    private bool isProgressIndeterminate;

    [ObservableProperty]
    private bool showProgressBar;

    [ObservableProperty]
    private string? currentVersionText;

    [ObservableProperty]
    private string? newVersionText;

    [GeneratedRegex(
        @"(##\s*(v[0-9]+\.[0-9]+\.[0-9]+(?:-(?:[0-9A-Za-z-.]+))?)((?:\n|.)+?))(?=(##\s*v[0-9]+\.[0-9]+\.[0-9]+)|\z)"
    )]
    private static partial Regex RegexChangelog();

    public UpdateViewModel(
        ILogger<UpdateViewModel> logger,
        ISettingsManager settingsManager,
        IHttpClientFactory httpClientFactory,
        IUpdateHelper updateHelper
    )
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.httpClientFactory = httpClientFactory;
        this.updateHelper = updateHelper;

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

        ReleaseNotes = await GetReleaseNotes(UpdateInfo.Changelog.ToString());
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

    internal async Task<string> GetReleaseNotes(string changelogUrl)
    {
        using var client = httpClientFactory.CreateClient();

        try
        {
            var response = await client.GetAsync(changelogUrl);
            if (response.IsSuccessStatusCode)
            {
                var changelog = await response.Content.ReadAsStringAsync();

                // Formatting for new changelog format
                // https://keepachangelog.com/en/1.1.0/
                if (changelogUrl.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    return FormatChangelog(
                            changelog,
                            Compat.AppVersion,
                            settingsManager.Settings.PreferredUpdateChannel
                        ) ?? "## Unable to format release notes";
                }

                return changelog;
            }

            return "## Unable to load release notes";
        }
        catch (HttpRequestException e)
        {
            return $"## Unable to fetch release notes ({e.StatusCode})\n\n[{changelogUrl}]({changelogUrl})";
        }
        catch (TaskCanceledException) { }

        return $"## Unable to fetch release notes\n\n[{changelogUrl}]({changelogUrl})";
    }

    /// <summary>
    /// Formats changelog markdown including up to the current version
    /// </summary>
    /// <param name="markdown">Markdown to format</param>
    /// <param name="currentVersion">Versions equal or below this are excluded</param>
    /// <param name="maxChannel">Maximum channel level to include</param>
    internal static string? FormatChangelog(
        string markdown,
        SemVersion currentVersion,
        UpdateChannel maxChannel = UpdateChannel.Stable
    )
    {
        var pattern = RegexChangelog();

        var results = pattern
            .Matches(markdown)
            .Select(
                m =>
                    new
                    {
                        Block = m.Groups[1].Value.Trim(),
                        Version = SemVersion.TryParse(
                            m.Groups[2].Value.Trim(),
                            SemVersionStyles.AllowV,
                            out var version
                        )
                            ? version
                            : null,
                        Content = m.Groups[3].Value.Trim()
                    }
            )
            .Where(x => x.Version is not null)
            .ToList();

        // Join all blocks until and excluding the current version
        // If we're on a pre-release, include the current release
        var currentVersionBlock = results.FindIndex(x => x.Version == currentVersion.WithoutMetadata());

        // For mismatching build metadata, add one
        if (
            currentVersionBlock != -1
            && results[currentVersionBlock].Version?.Metadata != currentVersion.Metadata
        )
        {
            currentVersionBlock++;
        }

        // Support for previous pre-release without changelogs
        if (currentVersionBlock == -1)
        {
            currentVersionBlock = results.FindIndex(
                x => x.Version == currentVersion.WithoutPrereleaseOrMetadata()
            );

            // Add 1 if found to include the current release
            if (currentVersionBlock != -1)
            {
                currentVersionBlock++;
            }
        }

        // Still not found, just include all
        if (currentVersionBlock == -1)
        {
            currentVersionBlock = results.Count;
        }

        // Filter out pre-releases
        var blocks = results
            .Take(currentVersionBlock)
            .Where(
                x =>
                    x.Version!.PrereleaseIdentifiers.Count == 0
                    || x.Version.PrereleaseIdentifiers[0].Value switch
                    {
                        "pre" when maxChannel >= UpdateChannel.Preview => true,
                        "dev" when maxChannel >= UpdateChannel.Development => true,
                        _ => false
                    }
            )
            .Select(x => x.Block);

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }
}
