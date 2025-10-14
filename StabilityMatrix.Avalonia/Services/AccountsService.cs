using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Client.SystemNetHttp;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Api.LykosAuthApi;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Services;
using static OpenIddict.Client.OpenIddictClientModels;
using ApiException = Refit.ApiException;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<IAccountsService, AccountsService>]
public class AccountsService : IAccountsService
{
    private readonly ILogger<AccountsService> logger;
    private readonly ISecretsManager secretsManager;
    private readonly ILykosAuthApiV1 lykosAuthApi;
    private readonly ILykosAuthApiV2 lykosAuthApiV2;
    private readonly ICivitTRPCApi civitTRPCApi;
    private readonly IHuggingFaceApi huggingFaceApi; // Added
    private readonly OpenIddictClientService openIdClient;

    /// <inheritdoc />
    public event EventHandler<LykosAccountStatusUpdateEventArgs>? LykosAccountStatusUpdate;

    /// <inheritdoc />
    public event EventHandler<CivitAccountStatusUpdateEventArgs>? CivitAccountStatusUpdate;

    /// <inheritdoc />
    public event EventHandler<HuggingFaceAccountStatusUpdateEventArgs>? HuggingFaceAccountStatusUpdate;

    public LykosAccountStatusUpdateEventArgs? LykosStatus { get; private set; }

    public CivitAccountStatusUpdateEventArgs? CivitStatus { get; private set; }

    public HuggingFaceAccountStatusUpdateEventArgs? HuggingFaceStatus { get; private set; }

    public AccountsService(
        ILogger<AccountsService> logger,
        ISecretsManager secretsManager,
        ILykosAuthApiV1 lykosAuthApi,
        ILykosAuthApiV2 lykosAuthApiV2,
        ICivitTRPCApi civitTRPCApi,
        IHuggingFaceApi huggingFaceApi, // Added
        OpenIddictClientService openIdClient
    )
    {
        this.logger = logger;
        this.secretsManager = secretsManager;
        this.lykosAuthApi = lykosAuthApi;
        this.lykosAuthApiV2 = lykosAuthApiV2;
        this.civitTRPCApi = civitTRPCApi;
        this.huggingFaceApi = huggingFaceApi; // Added
        this.openIdClient = openIdClient;

        // Update our own status when the Lykos account status changes
        LykosAccountStatusUpdate += (_, args) => LykosStatus = args;
        CivitAccountStatusUpdate += (_, args) => CivitStatus = args; // Assuming this was intended
        HuggingFaceAccountStatusUpdate += (_, args) => HuggingFaceStatus = args;
    }

    public async Task<bool> HasStoredLykosAccountAsync()
    {
        if (LykosStatus?.IsConnected == true)
        {
            return true;
        }

        var secrets = await secretsManager.SafeLoadAsync();

        return !string.IsNullOrEmpty(secrets.LykosAccountV2?.RefreshToken);
    }

    public async Task LykosLoginAsync(string email, string password)
    {
        var secrets = await secretsManager.SafeLoadAsync();

        var tokens = await lykosAuthApi.PostLogin(new PostLoginRequest(email, password));

        secrets = secrets with { LykosAccount = tokens };

        await secretsManager.SaveAsync(secrets);

        await RefreshLykosAsync(secrets);
    }

    public async Task LykosLoginViaGoogleOAuthAsync(string code, string state, string codeVerifier)
    {
        var secrets = await secretsManager.SafeLoadAsync();

        var tokens = await lykosAuthApi.GetOAuthGoogleCallback(code, state, codeVerifier);

        secrets = secrets with { LykosAccount = tokens };

        await secretsManager.SaveAsync(secrets);

        await RefreshLykosAsync(secrets);
    }

    public async Task LykosSignupAsync(string email, string password, string username)
    {
        var secrets = await secretsManager.SafeLoadAsync();

        var tokens = await lykosAuthApi.PostAccount(
            new PostAccountRequest(email, password, password, username)
        );

        secrets = secrets with { LykosAccount = tokens };

        await secretsManager.SaveAsync(secrets);

        await RefreshLykosAsync(secrets);
    }

    public async Task LykosLogoutAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        await secretsManager.SaveAsync(secrets with { LykosAccount = null });

        OnLykosAccountStatusUpdate(LykosAccountStatusUpdateEventArgs.Disconnected);
    }

    public async Task LykosAccountV2LoginAsync(LykosAccountV2Tokens tokens)
    {
        var secrets = await secretsManager.SafeLoadAsync();
        secrets = secrets with { LykosAccountV2 = tokens };
        await secretsManager.SaveAsync(secrets);

        await RefreshLykosAsync(secrets);
    }

    public async Task LykosAccountV2LogoutAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        await secretsManager.SaveAsync(secrets with { LykosAccountV2 = null });

        OnLykosAccountStatusUpdate(LykosAccountStatusUpdateEventArgs.Disconnected);
    }

    /// <inheritdoc />
    public async Task LykosPatreonOAuthLogoutAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        if (secrets.LykosAccountV2 is null)
        {
            throw new InvalidOperationException(
                "Lykos account must be connected in to manage OAuth connections"
            );
        }

        await lykosAuthApiV2.ApiV2OauthPatreon();

        await RefreshLykosAsync(secrets);
    }

    public async Task CivitLoginAsync(string apiToken)
    {
        var secrets = await secretsManager.SafeLoadAsync();

        // Get id first using the api token
        var userAccount = await civitTRPCApi.GetUserAccount(bearerToken: apiToken);
        var id =
            userAccount.InnerJson?.Id
            ?? throw new InvalidOperationException("GetUserAccount did not contain an id");

        // Then get the username using the id
        var account = await civitTRPCApi.GetUserById(new CivitGetUserByIdRequest { Id = id }, apiToken);
        var username = account.Result.Data.Json.Username;

        secrets = secrets with { CivitApi = new CivitApiTokens(apiToken, username) };

        await secretsManager.SaveAsync(secrets);

        await RefreshCivitAsync(secrets);
    }

    /// <inheritdoc />
    public async Task CivitLogoutAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        await secretsManager.SaveAsync(secrets with { CivitApi = null });

        OnCivitAccountStatusUpdate(CivitAccountStatusUpdateEventArgs.Disconnected);
    }

    public async Task RefreshAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();

        await RefreshLykosAsync(secrets);
        await RefreshCivitAsync(secrets);
        await RefreshHuggingFaceAsync(secrets);
    }

    public async Task RefreshLykosAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();

        await RefreshLykosAsync(secrets);
    }

    public async Task RefreshHuggingFaceAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        await RefreshHuggingFaceAsync(secrets);
    }

    private async Task RefreshLykosAsync(Secrets secrets)
    {
        if (
            secrets.LykosAccountV2 is not null
            && !string.IsNullOrWhiteSpace(secrets.LykosAccountV2?.RefreshToken)
            && !string.IsNullOrWhiteSpace(secrets.LykosAccountV2?.AccessToken)
        )
        {
            try
            {
                var user = await lykosAuthApiV2.ApiV2AccountsMe();

                ClaimsPrincipal? principal = null;

                if (secrets.LykosAccountV2.IdentityToken is not null)
                {
                    principal = secrets.LykosAccountV2.GetIdentityTokenPrincipal();
                }

                OnLykosAccountStatusUpdate(
                    new LykosAccountStatusUpdateEventArgs
                    {
                        IsConnected = true,
                        Principal = principal,
                        User = user,
                    }
                );

                return;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Timed out while fetching Lykos Auth user info");
            }
            catch (InvalidOperationException e)
            {
                logger.LogWarning(e, "Failed to get authentication token");
            }
            catch (ApiException e)
            {
                if (e.StatusCode is HttpStatusCode.Unauthorized) { }
                else
                {
                    logger.LogWarning(e, "Failed to get user info from Lykos");
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unknown error while refreshing Lykos account status");
            }
        }

        OnLykosAccountStatusUpdate(LykosAccountStatusUpdateEventArgs.Disconnected);
    }

    private async Task RefreshHuggingFaceAsync(Secrets secrets)
    {
        if (!string.IsNullOrWhiteSpace(secrets.HuggingFaceToken))
        {
            try
            {
                var response = await huggingFaceApi.GetCurrentUserAsync($"Bearer {secrets.HuggingFaceToken}");
                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    // Token is valid, user info fetched
                    logger.LogInformation(
                        "Hugging Face token is valid. User: {Username}",
                        response.Content.Name
                    );
                    OnHuggingFaceAccountStatusUpdate(
                        new HuggingFaceAccountStatusUpdateEventArgs(true, response.Content.Name)
                    );
                }
                else
                {
                    // Token is likely invalid or other API error
                    logger.LogWarning(
                        "Hugging Face token validation failed. Status: {StatusCode}, Error: {Error}, Content: {Content}",
                        response.StatusCode,
                        response.Error?.ToString(),
                        await response.Error?.GetContentAsAsync<string>() ?? "N/A"
                    );
                    OnHuggingFaceAccountStatusUpdate(
                        new HuggingFaceAccountStatusUpdateEventArgs(
                            false,
                            null,
                            $"Token validation failed: {response.StatusCode}"
                        )
                    );
                }
            }
            catch (ApiException apiEx)
            {
                // Handle Refit's ApiException (network issues, non-success status codes not caught by IsSuccessStatusCode if IApiResponse isn't used directly)
                logger.LogError(
                    apiEx,
                    "Hugging Face API request failed during token validation. Content: {Content}",
                    await apiEx.GetContentAsAsync<string>() ?? "N/A"
                );
                OnHuggingFaceAccountStatusUpdate(
                    new HuggingFaceAccountStatusUpdateEventArgs(
                        false,
                        null,
                        "API request failed during token validation."
                    )
                );
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                logger.LogError(ex, "An unexpected error occurred during Hugging Face token validation.");
                OnHuggingFaceAccountStatusUpdate(
                    new HuggingFaceAccountStatusUpdateEventArgs(false, null, "An unexpected error occurred.")
                );
            }
        }
        else
        {
            OnHuggingFaceAccountStatusUpdate(HuggingFaceAccountStatusUpdateEventArgs.Disconnected);
        }
    }

    private async Task RefreshCivitAsync(Secrets secrets)
    {
        if (secrets.CivitApi is not null)
        {
            try
            {
                var user = await civitTRPCApi.GetUserProfile(
                    new CivitUserProfileRequest { Username = secrets.CivitApi.Username },
                    secrets.CivitApi.ApiToken
                );

                OnCivitAccountStatusUpdate(
                    new CivitAccountStatusUpdateEventArgs { IsConnected = true, UserProfile = user }
                );

                return;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Timed out while fetching Civit Auth user info");
            }
            catch (ApiException e)
            {
                if (e.StatusCode is HttpStatusCode.Unauthorized) { }
                else
                {
                    logger.LogWarning(e, "Failed to get user info from Civit");
                }
            }
        }

        OnCivitAccountStatusUpdate(CivitAccountStatusUpdateEventArgs.Disconnected);
    }

    private void OnLykosAccountStatusUpdate(LykosAccountStatusUpdateEventArgs e)
    {
        if (!e.IsConnected && LykosStatus?.IsConnected == true)
        {
            logger.LogInformation("Lykos account disconnected");
        }
        else if (e.IsConnected && LykosStatus?.IsConnected == false)
        {
            logger.LogInformation(
                "Lykos account connected: {Id} ({Username})",
                e.User?.Id,
                e.Principal?.Identity?.Name
            );
        }

        LykosAccountStatusUpdate?.Invoke(this, e);
    }

    private void OnCivitAccountStatusUpdate(CivitAccountStatusUpdateEventArgs e)
    {
        if (!e.IsConnected && CivitStatus?.IsConnected == true)
        {
            logger.LogInformation("Civit account disconnected");
        }
        else if (e.IsConnected && CivitStatus?.IsConnected == false)
        {
            logger.LogInformation(
                "Civit account connected: {Id} ({Username})",
                e.UserProfile?.UserId,
                e.UserProfile?.Username
            );
        }

        CivitAccountStatusUpdate?.Invoke(this, e);
    }

    private void OnHuggingFaceAccountStatusUpdate(HuggingFaceAccountStatusUpdateEventArgs e)
    {
        if (!e.IsConnected && HuggingFaceStatus?.IsConnected == true)
        {
            logger.LogInformation("Hugging Face account disconnected");
        }
        else if (e.IsConnected && HuggingFaceStatus?.IsConnected == false)
        {
            // Assuming Username might be null for now as we are not fetching it.
            logger.LogInformation(
                "Hugging Face account connected"
                    + (string.IsNullOrWhiteSpace(e.Username) ? "" : $" (User: {e.Username})")
            );
        }
        else if (!e.IsConnected && !string.IsNullOrWhiteSpace(e.ErrorMessage))
        {
            logger.LogWarning("Hugging Face connection/validation failed: {ErrorMessage}", e.ErrorMessage);
        }
        HuggingFaceAccountStatusUpdate?.Invoke(this, e);
    }

    public async Task HuggingFaceLoginAsync(string token)
    {
        var secrets = await secretsManager.SafeLoadAsync();
        secrets = secrets with { HuggingFaceToken = token };
        await secretsManager.SaveAsync(secrets);
        await RefreshHuggingFaceAsync(secrets);
    }

    public async Task HuggingFaceLogoutAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        await secretsManager.SaveAsync(secrets with { HuggingFaceToken = null });
        OnHuggingFaceAccountStatusUpdate(HuggingFaceAccountStatusUpdateEventArgs.Disconnected);
    }
}
