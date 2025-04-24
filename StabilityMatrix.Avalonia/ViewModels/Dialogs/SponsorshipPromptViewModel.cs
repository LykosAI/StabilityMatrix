using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Settings;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(SponsorshipPromptDialog))]
[ManagedService]
[RegisterTransient<SponsorshipPromptViewModel>]
public partial class SponsorshipPromptViewModel(
    INavigationService<MainWindowViewModel> navigationService,
    INavigationService<SettingsViewModel> settingsNavService
) : TaskDialogViewModelBase
{
    [Localizable(false)]
    [ObservableProperty]
    private string titleEmoji = "\u2764\ufe0f";

    [ObservableProperty]
    private string title = Resources.Sponsorship_Title;

    [ObservableProperty]
    private string existingSupporterPreamble = Resources.Sponsorship_ExistingSupporterPreamble;

    [ObservableProperty]
    private string? featureText;

    [ObservableProperty]
    private bool isPatreonConnected;

    [ObservableProperty]
    private bool isExistingSupporter;

    public void Initialize(
        LykosAccountStatusUpdateEventArgs status,
        string featureName,
        string? requiredTier = null
    )
    {
        IsPatreonConnected = status.IsPatreonConnected;
        IsExistingSupporter = status.IsActiveSupporter;

        if (string.IsNullOrEmpty(requiredTier))
        {
            FeatureText = string.Format(Resources.Sponsorship_ReqAnyTier, featureName);
        }
        else
        {
            FeatureText = string.Format(Resources.Sponsorship_ReqSpecificTier, featureName, requiredTier);
        }
    }

    [RelayCommand]
    private async Task NavigateToAccountSettings()
    {
        CloseDialog(TaskDialogStandardResult.Close);
        navigationService.NavigateTo<SettingsViewModel>(new SuppressNavigationTransitionInfo());
        await Task.Delay(100);
        settingsNavService.NavigateTo<AccountSettingsViewModel>(new SuppressNavigationTransitionInfo());
    }

    [Localizable(false)]
    [RelayCommand]
    private static void OpenSupportUrl()
    {
        ProcessRunner.OpenUrl("https://www.patreon.com/join/StabilityMatrix");
    }

    public override TaskDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.Buttons =
        [
            new TaskDialogCommand
            {
                Text = Resources.Action_ViewSupportOptions,
                IsDefault = true,
                Command = OpenSupportUrlCommand
            },
            new TaskDialogButton
            {
                Text = Resources.Action_MaybeLater,
                DialogResult = TaskDialogStandardResult.Close
            }
        ];
        return dialog;
    }
}
