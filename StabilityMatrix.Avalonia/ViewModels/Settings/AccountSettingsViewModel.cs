using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Refit;
using StabilityMatrix.Avalonia.Diagnostics.LogViewer.Core.ViewModels;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.CivitTRPC;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(AccountSettingsPage))]
[Singleton, ManagedService]
public partial class AccountSettingsViewModel : PageViewModelBase
{
    private readonly ICivitTRPCApi civitTRPCApi;

    /// <inheritdoc />
    public override string Title => "Accounts";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Person, IsFilled = true };

    [ObservableProperty]
    private string? civitStatus;

    [ObservableProperty]
    private bool isCivitConnected;

    public AccountSettingsViewModel(ICivitTRPCApi civitTRPCApi)
    {
        this.civitTRPCApi = civitTRPCApi;
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        base.OnLoaded();

        if (Design.IsDesignMode)
        {
            return;
        }

        // RefreshCivitAccount().SafeFireAndForget();
    }

    private async Task RefreshCivitAccount()
    {
        var secrets = GlobalUserSecrets.LoadFromFile()!;

        if (secrets.CivitApiToken is null)
            return;

        var provisionalUser = Guid.NewGuid().ToString()[..8];

        try
        {
            var result = await civitTRPCApi.GetUserProfile(
                new CivitUserProfileRequest { Username = "ionite", Authed = true },
                secrets.CivitApiToken
            );

            CivitStatus = $"Connected with API Key as user '{result.Username}'";
        }
        catch (ApiException e)
        {
            Debug.WriteLine(e);
        }
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

        var secrets = GlobalUserSecrets.LoadFromFile()!;
        secrets.CivitApiToken = json;
        secrets.SaveToFile();

        // TODO
        await Task.Delay(1000);

        IsCivitConnected = true;
    }

    [RelayCommand]
    private async Task DisconnectCivit()
    {
        IsCivitConnected = false;
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
