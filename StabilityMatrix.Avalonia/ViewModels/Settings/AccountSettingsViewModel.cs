using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using OpenIddict.Client;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Api.LykosAuthApi;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;
using TeachingTip = StabilityMatrix.Core.Models.Settings.TeachingTip;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(AccountSettingsPage))]
[Singleton, ManagedService]
public partial class AccountSettingsViewModel : PageViewModelBase
{
    private readonly IAccountsService accountsService;
    private readonly ISettingsManager settingsManager;
    private readonly ServiceManager<ViewModelBase> vmFactory;
    private readonly INotificationService notificationService;
    private readonly ILykosAuthApiV2 lykosAuthApi;

    /// <inheritdoc />
    public override string Title => "Accounts";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Person, IconVariant = IconVariant.Filled };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectLykosCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectPatreonCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCivitCommand))]
    private bool isInitialUpdateFinished;

    [ObservableProperty]
    private string? lykosProfileImageUrl;

    [ObservableProperty]
    private bool isPatreonConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LykosProfileImageUrl))]
    private LykosAccountStatusUpdateEventArgs lykosStatus = LykosAccountStatusUpdateEventArgs.Disconnected;

    [ObservableProperty]
    private CivitAccountStatusUpdateEventArgs civitStatus = CivitAccountStatusUpdateEventArgs.Disconnected;

    public string LykosAccountManageUrl => new Uri(App.LykosAccountApiBaseUrl).Append("/manage").ToString();

    public AccountSettingsViewModel(
        IAccountsService accountsService,
        ISettingsManager settingsManager,
        ServiceManager<ViewModelBase> vmFactory,
        INotificationService notificationService,
        ILykosAuthApiV2 lykosAuthApi
    )
    {
        this.accountsService = accountsService;
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;
        this.notificationService = notificationService;
        this.lykosAuthApi = lykosAuthApi;

        accountsService.LykosAccountStatusUpdate += (_, args) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsInitialUpdateFinished = true;
                LykosStatus = args;
                IsPatreonConnected = args.IsPatreonConnected;
            });
        };

        accountsService.CivitAccountStatusUpdate += (_, args) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsInitialUpdateFinished = true;
                CivitStatus = args;
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

    private async Task<bool> BeforeConnectCheck()
    {
        // Show credentials storage notice if not seen
        if (!settingsManager.Settings.SeenTeachingTips.Contains(TeachingTip.AccountsCredentialsStorageNotice))
        {
            var dialog = new BetterContentDialog
            {
                Title = "About Account Credentials",
                Content = """
                    Account credentials and tokens are stored locally on your computer, with at-rest AES encryption. 
                    
                    If you make changes to your computer hardware, you may need to re-login to your accounts.
                    
                    Account tokens will not be viewable after saving, please make a note of them if you need to use them elsewhere.
                    """,
                PrimaryButtonText = Resources.Action_Continue,
                CloseButtonText = Resources.Action_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                MaxDialogWidth = 400
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            settingsManager.Transaction(
                s => s.SeenTeachingTips.Add(TeachingTip.AccountsCredentialsStorageNotice)
            );
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(IsInitialUpdateFinished))]
    private async Task ConnectLykos()
    {
        if (!await BeforeConnectCheck())
            return;

        var vm = vmFactory.Get<OAuthDeviceAuthViewModel>();
        vm.ChallengeRequest = new OpenIddictClientModels.DeviceChallengeRequest
        {
            ProviderName = OpenIdClientConstants.LykosAccount.ProviderName
        };
        await vm.ShowDialogAsync();

        if (vm.AuthenticationResult is { } result)
        {
            await accountsService.LykosAccountV2LoginAsync(
                new LykosAccountV2Tokens(result.AccessToken, result.RefreshToken, result.IdentityToken)
            );
        }
    }

    [RelayCommand]
    private Task DisconnectLykos()
    {
        return accountsService.LykosAccountV2LogoutAsync();
    }

    [RelayCommand(CanExecute = nameof(IsInitialUpdateFinished))]
    private async Task ConnectPatreon()
    {
        if (!await BeforeConnectCheck())
            return;

        if (LykosStatus.User?.Id is null)
            return;

        var urlResult = await notificationService.TryAsync(
            lykosAuthApi.ApiV2OauthPatreonLink(Program.MessagePipeUri.Append("/oauth/patreon/callback"))
        );

        if (!urlResult.IsSuccessful || urlResult.Result is not { } url)
        {
            return;
        }

        ProcessRunner.OpenUrl(urlResult.Result);

        var dialogVm = vmFactory.Get<OAuthConnectViewModel>();
        dialogVm.Title = "Connect Patreon Account";
        dialogVm.Url = url.ToString();

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

    [RelayCommand(CanExecute = nameof(IsInitialUpdateFinished))]
    private async Task ConnectCivit()
    {
        if (!await BeforeConnectCheck())
            return;

        var textFields = new TextBoxField[]
        {
            new()
            {
                Label = Resources.Label_ApiKey,
                Validator = s =>
                {
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        throw new ValidationException("API key is required");
                    }
                }
            }
        };

        var dialog = DialogHelper.CreateTextEntryDialog(
            "Connect CivitAI Account",
            """
            Login to [CivitAI](https://civitai.com/) and head to your [Account](https://civitai.com/user/account) page
            
            Add a new API key and paste it below
            """,
            "avares://StabilityMatrix.Avalonia/Assets/guide-civitai-api.webp",
            textFields
        );
        dialog.PrimaryButtonText = Resources.Action_Connect;

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || textFields[0].Text is not { } apiToken)
        {
            return;
        }

        var result = await notificationService.TryAsync(accountsService.CivitLoginAsync(apiToken));

        if (result.IsSuccessful)
        {
            await accountsService.RefreshAsync();
        }
    }

    [RelayCommand]
    private Task DisconnectCivit()
    {
        return accountsService.CivitLogoutAsync();
    }

    /// <summary>
    /// Update the Lykos profile image URL when the user changes.
    /// </summary>
    partial void OnLykosStatusChanged(LykosAccountStatusUpdateEventArgs value)
    {
        if (value.Email is { } userEmail)
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
}
