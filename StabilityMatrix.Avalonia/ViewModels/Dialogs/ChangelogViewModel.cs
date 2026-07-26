using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(ChangelogDialog))]
[ManagedService]
[RegisterTransient<ChangelogViewModel>]
public partial class ChangelogViewModel : ContentDialogViewModelBase
{
    private readonly ILogger<ChangelogViewModel> logger;
    private readonly IChangelogService changelogService;

    [ObservableProperty]
    public partial IReadOnlyList<ChangelogEntry>? Entries { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMarkdown))]
    public partial ChangelogEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public string? SelectedMarkdown => SelectedEntry?.Markdown;

    public string GitHubChangelogUrl => changelogService.GitHubChangelogUrl;

    public ChangelogViewModel(ILogger<ChangelogViewModel> logger, IChangelogService changelogService)
    {
        this.logger = logger;
        this.changelogService = changelogService;
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        IsLoading = true;
        HasError = false;

        try
        {
            var entries = await changelogService.GetChangelogEntries();
            Entries = entries;

            if (entries.Count == 0)
            {
                HasError = true;
                return;
            }

            SelectedEntry = SelectCurrentEntry(entries);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to load changelog entries");
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Selects the entry matching the running version, else the newest entry.
    /// </summary>
    private static ChangelogEntry SelectCurrentEntry(IReadOnlyList<ChangelogEntry> entries)
    {
        var current = Compat.AppVersion;

        return entries.FirstOrDefault(e => e.Version == current.WithoutMetadata())
            ?? entries.FirstOrDefault(e => e.Version == current.WithoutPrereleaseOrMetadata())
            ?? entries[0];
    }

    [RelayCommand]
    private void OpenGitHubChangelog()
    {
        ProcessRunner.OpenUrl(GitHubChangelogUrl);
    }
}
