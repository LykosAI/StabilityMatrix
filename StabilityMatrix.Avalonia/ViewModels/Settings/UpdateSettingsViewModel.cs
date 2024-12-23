using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exceptionless.DateTimeExtensions;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentIcons.Common;
using Injectio.Attributes;
using Semver;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(UpdateSettingsPage))]
[ManagedService]
[RegisterSingleton<UpdateSettingsViewModel>]
public partial class UpdateSettingsViewModel : PageViewModelBase
{
    private readonly IUpdateHelper updateHelper;
    private readonly IAccountsService accountsService;
    private readonly INavigationService<SettingsViewModel> settingsNavService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
    [NotifyPropertyChangedFor(nameof(HeaderText))]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private UpdateStatusChangedEventArgs? updateStatus;

    public bool IsUpdateAvailable => UpdateStatus?.LatestUpdate != null;

    public string HeaderText =>
        IsUpdateAvailable ? Resources.Label_UpdateAvailable : Resources.Label_YouAreUpToDate;

    public string? SubtitleText =>
        UpdateStatus is null
            ? null
            : string.Format(
                Resources.TextTemplate_LastChecked,
                UpdateStatus.CheckedAt.ToApproximateAgeString()
            );

    [ObservableProperty]
    private bool isAutoCheckUpdatesEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedUpdateChannelCard))]
    private UpdateChannel preferredUpdateChannel = UpdateChannel.Stable;

    public UpdateChannelCard? SelectedUpdateChannelCard
    {
        get => AvailableUpdateChannelCards.First(c => c.UpdateChannel == PreferredUpdateChannel);
        set => PreferredUpdateChannel = value?.UpdateChannel ?? UpdateChannel.Stable;
    }

    public IReadOnlyList<UpdateChannelCard> AvailableUpdateChannelCards { get; } =
        new UpdateChannelCard[]
        {
            new()
            {
                UpdateChannel = UpdateChannel.Development,
                Description = Resources.Label_UpdatesDevChannelDescription
            },
            new()
            {
                UpdateChannel = UpdateChannel.Preview,
                Description = Resources.Label_UpdatesPreviewChannelDescription
            },
            new() { UpdateChannel = UpdateChannel.Stable }
        };

    public UpdateSettingsViewModel(
        ISettingsManager settingsManager,
        IUpdateHelper updateHelper,
        IAccountsService accountsService,
        INavigationService<SettingsViewModel> settingsNavService
    )
    {
        this.updateHelper = updateHelper;
        this.accountsService = accountsService;
        this.settingsNavService = settingsNavService;

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.PreferredUpdateChannel,
            settings => settings.PreferredUpdateChannel,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsAutoCheckUpdatesEnabled,
            settings => settings.CheckForUpdates,
            true
        );

        accountsService.LykosAccountStatusUpdate += (_, args) =>
        {
            var isBetaChannelsEnabled = args.User?.IsActiveSupporter == true;

            foreach (
                var card in AvailableUpdateChannelCards.Where(c => c.UpdateChannel > UpdateChannel.Stable)
            )
            {
                card.IsSelectable = isBetaChannelsEnabled;
            }
        };

        // On update status changed
        updateHelper.UpdateStatusChanged += (_, args) =>
        {
            UpdateStatus = args;
        };
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        if (UpdateStatus is null)
        {
            await CheckForUpdates();
        }
        OnPropertyChanged(nameof(SubtitleText));
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        await updateHelper.CheckForUpdate();
    }

    /// <summary>
    /// Verify a new channel selection is valid, else returns false.
    /// </summary>
    /// <param name="card"></param>
    /// <returns></returns>
    public bool VerifyChannelSelection(UpdateChannelCard card)
    {
        if (card.UpdateChannel == UpdateChannel.Stable)
        {
            return true;
        }

        if (accountsService.LykosStatus?.User?.IsActiveSupporter == true)
        {
            return true;
        }

        return false;
    }

    public void ShowLoginRequiredDialog()
    {
        Dispatcher
            .UIThread.InvokeAsync(async () =>
            {
                var dialog = DialogHelper.CreateTaskDialog(
                    "Become a Supporter",
                    ""
                        + "Support the Stability Matrix Team and get access to early development builds and be the first to test new features. "
                );

                dialog.Buttons = new[]
                {
                    new(Resources.Label_Accounts, TaskDialogStandardResult.OK),
                    TaskDialogButton.CloseButton
                };

                dialog.Commands = new[]
                {
                    new TaskDialogCommand
                    {
                        Text = "Patreon",
                        Description = "https://patreon.com/StabilityMatrix",
                        Command = new RelayCommand(() =>
                        {
                            ProcessRunner.OpenUrl("https://patreon.com/StabilityMatrix");
                        })
                    }
                };

                if (await dialog.ShowAsync(true) is TaskDialogStandardResult.OK)
                {
                    settingsNavService.NavigateTo<AccountSettingsViewModel>(
                        new SuppressNavigationTransitionInfo()
                    );
                }
            })
            .SafeFireAndForget();
    }

    partial void OnUpdateStatusChanged(UpdateStatusChangedEventArgs? value)
    {
        // Update the update channel cards

        // Use maximum version from platforms equal or lower than current
        foreach (var card in AvailableUpdateChannelCards)
        {
            card.LatestVersion = value
                ?.UpdateChannels
                .Where(kv => kv.Key <= card.UpdateChannel)
                .Select(kv => kv.Value)
                .MaxBy(info => info.Version, SemVersion.PrecedenceComparer)
                ?.Version;
        }
    }

    partial void OnPreferredUpdateChannelChanged(UpdateChannel value)
    {
        CheckForUpdatesCommand.ExecuteAsync(null).SafeFireAndForget();
    }

    /// <inheritdoc />
    public override string Title => "Updates";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Settings, IconVariant = IconVariant.Filled };
}
