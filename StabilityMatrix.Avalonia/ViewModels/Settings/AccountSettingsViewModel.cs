using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Lykos;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(AccountSettingsPage))]
[Singleton, ManagedService]
public partial class AccountSettingsViewModel : PageViewModelBase
{
    private readonly IAccountsService accountsService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    /// <inheritdoc />
    public override string Title => "Accounts";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Person, IsFilled = true };

    [ObservableProperty]
    private string? civitStatus;

    [ObservableProperty]
    private bool isCivitConnected;

    [ObservableProperty]
    private bool isLykosConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LykosProfileImageUrl))]
    private GetUserResponse? lykosUser;

    [ObservableProperty]
    private string? lykosProfileImageUrl;

    partial void OnLykosUserChanged(GetUserResponse? value)
    {
        if (value?.Id is { } userEmail)
        {
            userEmail = userEmail.Trim().ToLowerInvariant();

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(userEmail));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            LykosProfileImageUrl = $"https://gravatar.com/avatar/{hash}?s=512&d=retro";
        }
    }

    public AccountSettingsViewModel(
        IAccountsService accountsService,
        ServiceManager<ViewModelBase> vmFactory
    )
    {
        this.accountsService = accountsService;
        this.vmFactory = vmFactory;

        accountsService.LykosAccountStatusUpdate += (_, args) =>
        {
            IsLykosConnected = args.IsConnected;
            LykosUser = args.User;
        };
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        await accountsService.RefreshAsync();
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
