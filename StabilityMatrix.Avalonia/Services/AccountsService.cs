using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.CivitTRPC;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Services;
using ApiException = Refit.ApiException;

namespace StabilityMatrix.Avalonia.Services;

[Singleton(typeof(IAccountsService))]
public class AccountsService : IAccountsService
{
    private readonly ILogger<AccountsService> logger;
    private readonly ISecretsManager secretsManager;
    private readonly ILykosAuthApi lykosAuthApi;
    private readonly ICivitTRPCApi civitTRPCApi;

    /// <inheritdoc />
    public event EventHandler<LykosAccountStatusUpdateEventArgs>? LykosAccountStatusUpdate;

    /// <inheritdoc />
    public event EventHandler<CivitAccountStatusUpdateEventArgs>? CivitAccountStatusUpdate;

    public LykosAccountStatusUpdateEventArgs? LykosStatus { get; private set; }

    public CivitAccountStatusUpdateEventArgs? CivitStatus { get; private set; }

    public AccountsService(
        ILogger<AccountsService> logger,
        ISecretsManager secretsManager,
        ILykosAuthApi lykosAuthApi,
        ICivitTRPCApi civitTRPCApi
    )
    {
        this.logger = logger;
        this.secretsManager = secretsManager;
        this.lykosAuthApi = lykosAuthApi;
        this.civitTRPCApi = civitTRPCApi;

        // Update our own status when the Lykos account status changes
        LykosAccountStatusUpdate += (_, args) => LykosStatus = args;
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

    /// <inheritdoc />
    public async Task LykosPatreonOAuthLogoutAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync();
        if (secrets.LykosAccount is null)
        {
            throw new InvalidOperationException(
                "Lykos account must be connected in to manage OAuth connections"
            );
        }

        await lykosAuthApi.DeletePatreonOAuth();

        await RefreshLykosAsync(secrets);
    }

    public async Task CivitLoginAsync(string apiToken)
    {
        var secrets = await secretsManager.SafeLoadAsync();

        // Get id first using the api token
        var userAccount = await civitTRPCApi.GetUserAccountDefault(apiToken);
        var id = userAccount.Result.Data.Json.Id;

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
    }

    private async Task RefreshLykosAsync(Secrets secrets)
    {
        if (
            secrets.LykosAccount is not null
            && !string.IsNullOrWhiteSpace(secrets.LykosAccount?.RefreshToken)
            && !string.IsNullOrWhiteSpace(secrets.LykosAccount?.AccessToken)
        )
        {
            try
            {
                var user = await lykosAuthApi.GetUserSelf();

                OnLykosAccountStatusUpdate(
                    new LykosAccountStatusUpdateEventArgs { IsConnected = true, User = user }
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
                e.User?.Account.Name
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
}
