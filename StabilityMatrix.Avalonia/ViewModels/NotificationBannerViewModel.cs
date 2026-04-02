using System.ComponentModel;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Notifications;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels;

[ManagedService]
[View(typeof(NotificationBanner))]
[RegisterTransient<NotificationBannerViewModel>]
public partial class NotificationBannerViewModel(IAppNotificationService appNotificationService)
    : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = "";

    [ObservableProperty]
    public partial string ActionLabel { get; set; } = "";

    [ObservableProperty]
    [Localizable(false)]
    public partial string Variant { get; set; } = "info";

    [ObservableProperty]
    public partial InfoBarSeverity Severity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsDismissible { get; set; } = true;

    [ObservableProperty]
    public partial bool HasAction { get; set; }

    /// <summary>
    /// The underlying notification data.
    /// </summary>
    public AppNotification? Notification { get; private set; }

    /// <summary>
    /// Populate the banner from a notification and make it visible.
    /// Must be called on UI thread.
    /// </summary>
    public void Show(AppNotification notification)
    {
        Dispatcher.UIThread.VerifyAccess();

        Notification = notification;
        Message = appNotificationService.ResolveLocalizedString(notification.Message) ?? "";
        Variant = notification.Style.Variant;
        Severity = notification.Style.Variant switch
        {
            "warning" => InfoBarSeverity.Warning,
            "success" => InfoBarSeverity.Success,
            "error" => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };
        IsDismissible = notification.Dismissible;
        HasAction = notification.Action is not null;

        if (notification.Action is { } action)
        {
            ActionLabel = appNotificationService.ResolveLocalizedString(action.Label) ?? "";
        }

        IsVisible = true;
    }

    [RelayCommand]
    private async Task ActionClicked()
    {
        if (Notification?.Action is not { } action)
            return;

        switch (action.Type)
        {
            case AppNotificationActionType.Url when action.Url is not null:
                ProcessRunner.OpenUrl(action.Url);
                break;

            case AppNotificationActionType.Dialog when Notification.Dialog is { } dialogData:
                await ShowNotificationDialogAsync(dialogData);
                break;
        }
    }

    [RelayCommand]
    private void DismissClicked()
    {
        if (Notification is null)
            return;

        appNotificationService.Dismiss(Notification.Id);
        IsVisible = false;
    }

    private async Task ShowNotificationDialogAsync(AppNotificationDialog dialogData)
    {
        Dispatcher.UIThread.VerifyAccess();

        var title = appNotificationService.ResolveLocalizedString(dialogData.Title) ?? "";
        var content = appNotificationService.ResolveLocalizedString(dialogData.Content) ?? "";

        var dialog = DialogHelper.CreateMarkdownDialog(content, title, styleClass: "NotificationDialog");
        dialog.MinDialogWidth = 700;
        dialog.MaxDialogWidth = 900;
        dialog.MaxDialogHeight = 800;
        dialog.ContentMargin = new Thickness(16, 8);

        // If custom buttons are defined, configure them
        if (dialogData.Buttons is { Count: > 0 } buttons)
        {
            // First button -> Primary
            if (buttons.Count >= 1)
            {
                var btn = buttons[0];
                dialog.PrimaryButtonText = appNotificationService.ResolveLocalizedString(btn.Label) ?? "";
                dialog.IsPrimaryButtonEnabled = true;
            }

            // Second button -> Close
            if (buttons.Count >= 2)
            {
                var btn = buttons[1];
                dialog.CloseButtonText = appNotificationService.ResolveLocalizedString(btn.Label) ?? "";
            }
        }
        else
        {
            dialog.CloseButtonText = Resources.Action_Close;
        }

        var result = await dialog.ShowAsync();

        // Handle button actions
        if (dialogData.Buttons is { Count: > 0 } actionButtons)
        {
            var clickedButton = result switch
            {
                ContentDialogResult.Primary when actionButtons.Count >= 1 => actionButtons[0],
                ContentDialogResult.None when actionButtons.Count >= 2 => actionButtons[1],
                _ => null,
            };

            if (clickedButton is not null)
            {
                // Open URL if applicable
                if (clickedButton.Type == AppNotificationButtonType.Url && clickedButton.Url is not null)
                {
                    ProcessRunner.OpenUrl(clickedButton.Url);
                }

                // Dismiss if applicable
                if (clickedButton.Type == AppNotificationButtonType.Dismiss || clickedButton.DismissOnClick)
                {
                    DismissClicked();
                }
            }
        }
    }
}
