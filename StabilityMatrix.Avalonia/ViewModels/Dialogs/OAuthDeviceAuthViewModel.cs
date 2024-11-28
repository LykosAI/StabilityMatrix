using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// ViewModel for OAuth Device Authentication
/// </summary>
[View(typeof(OAuthDeviceAuthDialog))]
[ManagedService]
[Transient]
public partial class OAuthDeviceAuthViewModel(
    ILogger<OAuthDeviceAuthViewModel> logger,
    OpenIddictClientService openIdClient
) : TaskDialogViewModelBase
{
    private CancellationTokenSource authenticationCts = new();

    public OpenIddictClientModels.DeviceChallengeRequest? ChallengeRequest { get; set; }

    public OpenIddictClientModels.DeviceChallengeResult? ChallengeResult { get; private set; }

    public OpenIddictClientModels.DeviceAuthenticationRequest? AuthenticationRequest { get; private set; }

    public OpenIddictClientModels.DeviceAuthenticationResult? AuthenticationResult { get; private set; }

    public virtual string? ServiceName => ChallengeRequest?.ProviderName;

    [ObservableProperty]
    private string? description = Resources.Text_OAuthDeviceAuthDescription;

    [ObservableProperty]
    private Uri? verificationUri;

    [ObservableProperty]
    private string? userCode;

    [ObservableProperty]
    private bool isLoading;

    public override TaskDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.Title = string.Format(Resources.TextTemplate_OAuthLoginTitle, ServiceName);
        dialog.Header = dialog.Title;
        dialog.Buttons =
        [
            GetCommandButton(Resources.Action_CopyAndOpen, CopyCodeAndOpenUrlCommand),
            GetCloseButton(Resources.Action_Cancel)
        ];
        return dialog;
    }

    [RelayCommand]
    private async Task CopyCodeAndOpenUrl()
    {
        if (VerificationUri is null)
            return;

        try
        {
            await App.Clipboard.SetTextAsync(UserCode);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to copy user code to clipboard");
        }

        ProcessRunner.OpenUrl(VerificationUri);
    }

    /*/// <summary>
    /// Prompt to authenticate with the service using a dialog
    /// </summary>
    public async Task<OpenIddictClientModels.DeviceAuthenticationResult?> AuthenticateWithDialogAsync()
    {
        using var cts = new CancellationTokenSource();

        var dialogTask = ShowDialogAsync();

        await TryAuthenticateAsync(ChallengeRequest!, cts.Token);

        var result = await dialogTask;
        await cts.CancelAsync();

        return result switch
        {
            TaskDialogStandardResult.OK => AuthenticationResult,
            _ => null
        };
    }*/

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        if (!Design.IsDesignMode && ChallengeRequest is not null && ChallengeResult is null)
        {
            await TryAuthenticateAsync();
        }
    }

    protected override void OnDialogClosing(object? sender, TaskDialogClosingEventArgs e)
    {
        base.OnDialogClosing(sender, e);

        authenticationCts.Cancel();
    }

    public async Task ChallengeAsync()
    {
        if (ChallengeRequest is null)
        {
            throw new InvalidOperationException(
                "ChallengeRequest must be set before calling StartChallengeAsync"
            );
        }

        ChallengeResult = await openIdClient.ChallengeUsingDeviceAsync(ChallengeRequest);

        UserCode = ChallengeResult.DeviceCode;
        VerificationUri = ChallengeResult.VerificationUri;
    }

    public async Task TryAuthenticateAsync()
    {
        if (ChallengeRequest is null)
        {
            throw new InvalidOperationException("ChallengeRequest must be set");
        }

        try
        {
            // Get challenge result
            ChallengeResult = await openIdClient.ChallengeUsingDeviceAsync(ChallengeRequest);

            UserCode = ChallengeResult.UserCode;
            VerificationUri = ChallengeResult.VerificationUri;

            // Wait for user to complete auth
            var result = await openIdClient.AuthenticateWithDeviceAsync(
                new OpenIddictClientModels.DeviceAuthenticationRequest
                {
                    DeviceCode = ChallengeResult.DeviceCode,
                    Interval = ChallengeResult.Interval,
                    Timeout = ChallengeResult.ExpiresIn,
                    CancellationToken = authenticationCts.Token
                }
            );

            logger.LogInformation("Device authentication completed");
            AuthenticationResult = result;
            CloseDialog(TaskDialogStandardResult.OK);
        }
        catch (OperationCanceledException e)
        {
            logger.LogInformation(e, "Device authentication was cancelled");
            AuthenticationResult = null;
            CloseDialog(TaskDialogStandardResult.Close);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Device authentication error");
            AuthenticationResult = null;
            await CloseDialogWithErrorResultAsync(e.Message);
        }
    }

    private async Task CloseDialogWithErrorResultAsync(string message)
    {
        var dialog = DialogHelper.CreateMarkdownDialog(message, Resources.Label_ConnectAccountFailed);

        await dialog.ShowAsync();

        CloseDialog(TaskDialogStandardResult.Close);
    }
}
