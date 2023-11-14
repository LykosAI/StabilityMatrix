using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Processes;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(AccountSettingsPage))]
[Singleton, ManagedService]
public partial class AccountSettingsViewModel : PageViewModelBase
{
    private readonly IAccountsService accountsService;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly INotificationService notificationService;
    private readonly ILykosAuthApi lykosAuthApi;

    /// <inheritdoc />
    public override string Title => "Accounts";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Person, IsFilled = true };

    [ObservableProperty]
    private bool isLykosConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LykosProfileImageUrl))]
    private GetUserResponse? lykosUser;

    [ObservableProperty]
    private string? lykosProfileImageUrl;

    [ObservableProperty]
    private bool isPatreonConnected;

    [ObservableProperty]
    private bool isCivitConnected;

    partial void OnLykosUserChanged(GetUserResponse? value)
    {
        if (value?.Id is { } userEmail)
        {
            userEmail = userEmail.Trim().ToLowerInvariant();

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(userEmail));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            LykosProfileImageUrl = $"https://gravatar.com/avatar/{hash}?s=512&d=retro";
        }
        else
        {
            LykosProfileImageUrl = null;
        }
    }

    public AccountSettingsViewModel(
        IAccountsService accountsService,
        ServiceManager<ViewModelBase> vmFactory,
        INotificationService notificationService,
        ILykosAuthApi lykosAuthApi
    )
    {
        this.accountsService = accountsService;
        this.vmFactory = vmFactory;
        this.notificationService = notificationService;
        this.lykosAuthApi = lykosAuthApi;

        accountsService.LykosAccountStatusUpdate += (_, args) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsLykosConnected = args.IsConnected;
                LykosUser = args.User;
                IsPatreonConnected = args.IsPatreonConnected;
            });
        };
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        base.OnLoaded();

        if (Design.IsDesignMode)
        {
            return;
        }

        accountsService.RefreshAsync().SafeFireAndForget();
    }

    [RelayCommand]
    private async Task ConnectCivit()
    {
        var textFields = new TextBoxField[] { new() { Label = "API Key" } };

        var dialog = DialogHelper.CreateTextEntryDialog("Connect CivitAI Account", "", textFields);

        if (
            await dialog.ShowAsync() != ContentDialogResult.Primary
            || textFields[0].Text is not { } json
        )
        {
            return;
        }

        // TODO
        await Task.Delay(200);

        IsCivitConnected = true;
    }

    [RelayCommand]
    private async Task DisconnectCivit()
    {
        await Task.Yield();

        IsCivitConnected = false;
    }

    [RelayCommand]
    private async Task ConnectLykos()
    {
        var vm = vmFactory.Get<LykosLoginViewModel>();
        if (await vm.ShowDialogAsync() == TaskDialogStandardResult.OK)
        {
            IsLykosConnected = true;
            await accountsService.RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task DisconnectLykos()
    {
        await accountsService.LykosLogoutAsync();
    }

    [RelayCommand]
    private async Task ConnectPatreon()
    {
        if (LykosUser?.Id is null)
            return;

        var urlResult = await notificationService.TryAsync(
            lykosAuthApi.GetPatreonOAuthUrl(
                Program.MessagePipeUri.Append("/oauth/patreon/callback").ToString()
            )
        );

        if (!urlResult.IsSuccessful || urlResult.Result is not { } url)
        {
            return;
        }

        ProcessRunner.OpenUrl(urlResult.Result);

        var dialogVm = vmFactory.Get<OAuthConnectViewModel>();
        dialogVm.Title = "Connect Patreon Account";
        dialogVm.Url = url;

        if (await dialogVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            await accountsService.RefreshAsync();

            // Bring main window to front since browser is probably covering
            var main = App.TopLevel as Window;
            main?.Activate();
        }
    }

    [RelayCommand]
    private async Task DisconnectPatreon()
    {
        await notificationService.TryAsync(accountsService.LykosPatreonOAuthLogoutAsync());
    }

    /*[RelayCommand]
    private async Task ConnectCivitAccountOld()
    {
        var textFields = new TextBoxField[] { new() { Label = "API Key" } };

        var dialog = DialogHelper.CreateTextEntryDialog("Connect CivitAI Account", "", textFields);

        if (
            await dialog.ShowAsync() != ContentDialogResult.Primary
            || textFields[0].Text is not { } json
        )
        {
            return;
        }

        var secrets = GlobalUserSecrets.LoadFromFile()!;
        secrets.CivitApiToken = json;
        secrets.SaveToFile();

        RefreshCivitAccount().SafeFireAndForget();
    }*/
}
