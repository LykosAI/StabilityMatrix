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
using Injectio.Attributes;
using Microsoft.Extensions.Options;
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
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;
using TeachingTip = StabilityMatrix.Core.Models.Settings.TeachingTip;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(AccountSettingsPage))]
[ManagedService]
[RegisterSingleton<AccountSettingsViewModel>]
public partial class AccountSettingsViewModel : PageViewModelBase
{
    private readonly IAccountsService accountsService;
    private readonly ISettingsManager settingsManager;
    private readonly IServiceManager<ViewModelBase> vmFactory;
    private readonly INotificationService notificationService;
    private readonly ILykosAuthApiV2 lykosAuthApi;
    private readonly IOptions<ApiOptions> apiOptions;

    /// <inheritdoc />
    public override string Title => "Accounts";

    /// <inheritdoc />
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Person, IconVariant = IconVariant.Filled };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectLykosCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectPatreonCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCivitCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectHuggingFaceCommand))]
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

    // Assume HuggingFaceAccountStatusUpdateEventArgs will be created with at least these properties
    // For now, using a placeholder or assuming a structure like:
    // public record HuggingFaceAccountStatusUpdateEventArgs(bool IsConnected, string? Username);
    // Initialize with a disconnected state.
    [ObservableProperty]
    private HuggingFaceAccountStatusUpdateEventArgs huggingFaceStatus = new(false, null);

    [ObservableProperty]
    private bool isHuggingFaceConnected;

    [ObservableProperty]
    private string huggingFaceUsernameWithParentheses = string.Empty;

    public string LykosAccountManageUrl =>
        apiOptions.Value.LykosAccountApiBaseUrl.Append("/manage").ToString();

    public AccountSettingsViewModel(
        IAccountsService accountsService,
        ISettingsManager settingsManager,
        IServiceManager<ViewModelBase> vmFactory,
        INotificationService notificationService,
        ILykosAuthApiV2 lykosAuthApi,
        IOptions<ApiOptions> apiOptions
    )
    {
        this.accountsService = accountsService;
        this.settingsManager = settingsManager;
        this.vmFactory = vmFactory;
        this.notificationService = notificationService;
        this.lykosAuthApi = lykosAuthApi;
        this.apiOptions = apiOptions;

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

        accountsService.HuggingFaceAccountStatusUpdate += (_, args) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsInitialUpdateFinished = true;
                HuggingFaceStatus = args;
                // IsHuggingFaceConnected and HuggingFaceUsernameWithParentheses will be updated by OnHuggingFaceStatusChanged
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
                MaxDialogWidth = 400,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            settingsManager.Transaction(s =>
                s.SeenTeachingTips.Add(TeachingTip.AccountsCredentialsStorageNotice)
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
            ProviderName = OpenIdClientConstants.LykosAccount.ProviderName,
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
                },
            },
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

    [RelayCommand(CanExecute = nameof(IsInitialUpdateFinished))]
    private async Task ConnectHuggingFace()
    {
        if (!await BeforeConnectCheck())
            return;

        var fields = new[]
        {
            new TextBoxField("Hugging Face Token", isPassword: true, validator: s =>
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    throw new ValidationException("Token is required");
                }
            })
        };

        var (result, values) = await DialogHelper.CreateTextEntryDialog(
            "Connect Hugging Face Account",
            "Go to [Hugging Face settings](https://huggingface.co/settings/tokens) to create a new Access Token. Ensure it has read permissions. Paste the token below.",
            fields,
            image: null // Or a relevant image if available
        );

        if (result == DialogResult.Primary && values.TryGetValue("Hugging Face Token", out var token))
        {
            // Assuming HuggingFaceLoginAsync will be added to IAccountsService
            await accountsService.HuggingFaceLoginAsync(token);
            // Optionally refresh:
            await accountsService.RefreshAsync(); 
            // or await accountsService.RefreshHuggingFaceAsync(); // if a specific refresh is implemented
        }
    }

    [RelayCommand]
    private Task DisconnectHuggingFace()
    {
        // Assuming HuggingFaceLogoutAsync will be added to IAccountsService
        return accountsService.HuggingFaceLogoutAsync();
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

    partial void OnHuggingFaceStatusChanged(HuggingFaceAccountStatusUpdateEventArgs value)
    {
        IsHuggingFaceConnected = value.IsConnected;

        if (value.IsConnected)
        {
            if (!string.IsNullOrWhiteSpace(value.Username))
            {
                HuggingFaceUsernameWithParentheses = $"({value.Username})";
            }
            else
            {
                HuggingFaceUsernameWithParentheses = "(Connected)"; // Fallback if no username
            }
        }
        else
        {
            HuggingFaceUsernameWithParentheses = string.Empty;
            if (!string.IsNullOrWhiteSpace(value.ErrorMessage))
            {
                // Assuming INotificationService.Show takes these parameters and NotificationType.Error is valid.
                // Dispatcher.UIThread.Post might be needed if Show itself doesn't handle UI thread marshalling,
                // but usually notification services are designed to be called from any thread.
                // The event handler for HuggingFaceAccountStatusUpdate already posts to UIThread,
                // so this method (OnHuggingFaceStatusChanged) is already on the UI thread.
                notificationService.Show(
                    "Hugging Face Connection Error",
                    $"Failed to connect Hugging Face account: {value.ErrorMessage}. Please check your token and try again.",
                    NotificationType.Error, // Assuming NotificationType.Error exists and is correct
                    TimeSpan.FromSeconds(10) // Display for 10 seconds, or TimeSpan.Zero for persistent
                );
            }
        }
    }
}

// Placeholder for the event args class, actual definition will be in Core project
// namespace StabilityMatrix.Core.Services
// {
//     public record HuggingFaceAccountStatusUpdateEventArgs(bool IsConnected, string? Username, string? ErrorMessage = null); 
// }
