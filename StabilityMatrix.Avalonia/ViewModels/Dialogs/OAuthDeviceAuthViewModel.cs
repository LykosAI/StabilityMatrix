using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using PropertyModels.Extensions;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using Windows.ApplicationModel;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(OAuthDeviceAuthDialog))]
[ManagedService]
[Transient]
public partial class OAuthDeviceAuthViewModel(
    ILogger<OAuthDeviceAuthViewModel> logger,
    OpenIddictClientService openIdClient
) : ContentDialogViewModelBase
{
    public OpenIddictClientModels.DeviceChallengeRequest? ChallengeRequest { get; set; }

    public OpenIddictClientModels.DeviceChallengeResult? ChallengeResult { get; set; }

    public OpenIddictClientModels.DeviceAuthenticationResult AuthenticationResult { get; set; }

    public string? Description { get; set; }

    public string? ServiceName { get; set; }

    [ObservableProperty]
    private Uri? verificationUri;

    [ObservableProperty]
    private string? deviceCode;

    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Prompt to authenticate with the service using a dialog
    /// </summary>
    public async Task<OpenIddictClientModels.DeviceAuthenticationResult?> AuthenticateWithDialogAsync()
    {
        var dialog = new BetterContentDialog
        {
            Title = ServiceName,
            Content = this,
            CloseButtonText = Resources.Action_Cancel
        };

        using var cts = new CancellationTokenSource();

        var result = await dialog.ShowAsync();
        await cts.CancelAsync();

        return result switch
        {
            ContentDialogResult.Primary => AuthenticationResult,
            _ => null
        };
    }

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();
        
        if (Design.IsDess)asdasd
        

          if (!Design.IsDesignMode && ChallengeRequest is not null && ChallengeResult is null)
        {
            await StartChallengeAsync();
        }
    }

    public override Task OnUnloadedAsync()
    {
        return base.OnUnloadedAsync();
    }

    private async Task StartChallengeAsync()
    {
        if (ChallengeRequest is null)
        {
            throw new InvalidOperationException(
                "ChallengeRequest must be set before calling StartChallengeAsync"
            );
        }

        ChallengeResult = await openIdClient.ChallengeUsingDeviceAsync(ChallengeRequest);

        DeviceCode = ChallengeResult.DeviceCode;
        VerificationUri = ChallengeResult.VerificationUri;
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (ChallengeResult is null)
        {
            throw new InvalidOperationException(
                "ChallengeResult must be set before calling AuthenticateAsync"
            );
        }

        AuthenticationResult = await openIdClient.AuthenticateWithDeviceAsync(
            new OpenIddictClientModels.DeviceAuthenticationRequest
            {
                DeviceCode = ChallengeResult.DeviceCode,
                Interval = ChallengeResult.Interval,
                Timeout = ChallengeResult.ExpiresIn,
                CancellationToken = cancellationToken
            }
        );
    }

    private async Task TryAuthenticateUsingDeviceAsync(
        OpenIddictClientModels.DeviceChallengeRequest challengeRequest,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var challenge = await openIdClient.ChallengeUsingDeviceAsync(challengeRequest);

            DeviceCode = challenge.DeviceCode;
            VerificationUri = challenge.VerificationUri;

            // Wait for user to complete auth
            var result = await openIdClient.AuthenticateWithDeviceAsync(
                new OpenIddictClientModels.DeviceAuthenticationRequest
                {
                    DeviceCode = challenge.DeviceCode,
                    Interval = challenge.Interval,
                    Timeout = challenge.ExpiresIn,
                    CancellationToken = cancellationToken
                }
            );
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var dialog = DialogHelper.CreateMarkdownDialog(content, Resources.Label_ConnectAccountFailed);

                dialog
                    .ShowAsync()
                    .ContinueWith(_ => OnCloseButtonClick(), CancellationToken.None)
                    .SafeFireAndForget();
            });
        }
    }
}
