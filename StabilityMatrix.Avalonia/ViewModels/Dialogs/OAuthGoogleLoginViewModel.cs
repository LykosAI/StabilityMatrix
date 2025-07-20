﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using DeviceId.Encoders;
using Injectio.Attributes;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSec.Cryptography;
using Refit;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[RegisterTransient<OAuthGoogleLoginViewModel>]
[ManagedService]
[View(typeof(OAuthLoginDialog))]
public class OAuthGoogleLoginViewModel(
    ILogger<OAuthConnectViewModel> baseLogger,
    IDistributedSubscriber<string, Uri> uriHandlerSubscriber,
    ILogger<OAuthGoogleLoginViewModel> logger,
    ILykosAuthApiV1 lykosAuthApi,
    IAccountsService accountsService,
    IOptions<ApiOptions> apiOptions
) : OAuthLoginViewModel(baseLogger, uriHandlerSubscriber)
{
    private string? challenge;
    private string? verifier;
    private string? state;

    public override Uri? IconUri { get; set; } =
        new("avares://StabilityMatrix.Avalonia/Assets/brands-google-oauth-icon.svg");

    // ReSharper disable once LocalizableElement
    public override string ServiceName { get; set; } = "Google";

    // ReSharper disable once LocalizableElement
    public override string CallbackUriPath { get; set; } = "/oauth/google/callback";

    protected override async Task OnCallbackUriMatchedAsync(Uri uri)
    {
        IsLoading = true;

        try
        {
            // Bring the app to the front
            (App.TopLevel as Window)?.Activate();

            if (string.IsNullOrEmpty(verifier))
            {
                // ReSharper disable once LocalizableElement
                throw new InvalidOperationException("Verifier is not set");
            }

            var response = GoogleOAuthResponse.ParseFromQueryString(uri.Query);

            if (!string.IsNullOrEmpty(response.Error))
            {
                logger.LogWarning("Response has error: {Error}", response.Error);
                OnLoginFailed([("Error", response.Error)]);
                return;
            }

            if (string.IsNullOrEmpty(response.Code) || string.IsNullOrEmpty(response.State))
            {
                logger.LogWarning("Response missing code or state: {Uri}", uri.RedactQueryValues());
                OnLoginFailed([("Invalid Response", "code and state are required")]);
                return;
            }

            if (response.State != state)
            {
                logger.LogWarning("Response state mismatch: {Uri}", uri.RedactQueryValues());
                OnLoginFailed([("Invalid Response", "state mismatch")]);
                return;
            }

            await accountsService.LykosLoginViaGoogleOAuthAsync(response.Code, response.State, verifier);

            // Success
            OnPrimaryButtonClick();
        }
        catch (ApiException e)
        {
            logger.LogError(e, "Api error while handling callback uri");

            OnLoginFailed([(e.StatusCode.ToString(), e.Content)]);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to handle callback uri");

            OnLoginFailed([(e.GetType().Name, e.Message)]);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        IsLoading = true;

        try
        {
            await GenerateUrlAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to generate url");

            OnLoginFailed([(e.GetType().Name, e.Message)]);

            return;
        }
        finally
        {
            IsLoading = false;
        }

        // Open in browser
        ProcessRunner.OpenUrl(Url!);
    }

    private async Task GenerateUrlAsync()
    {
        (challenge, verifier) = GeneratePkceSha256ChallengePair();

        var redirectUri = apiOptions.Value.LykosAuthApiBaseUrl.Append("/api/open/sm/oauth/google/callback");

        logger.LogDebug("Requesting Google OAuth URL...");

        var link = await lykosAuthApi.GetOAuthGoogleLoginOrSignupLink(
            redirectUri.ToString(),
            codeChallenge: challenge,
            codeChallengeMethod: "S256"
        );

        var queryCollection = HttpUtility.ParseQueryString(link.Query);
        // ReSharper disable once LocalizableElement
        state = queryCollection.Get("state");

        Url = link.ToString();

        logger.LogInformation("Generated Google OAuth URL: {Url}", Url);
    }
}
