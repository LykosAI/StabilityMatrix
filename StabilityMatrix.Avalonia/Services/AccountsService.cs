using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Lykos;
using ApiException = Refit.ApiException;

namespace StabilityMatrix.Avalonia.Services;

[Singleton(typeof(IAccountsService))]
public class AccountsService : IAccountsService
{
    private readonly ILogger<AccountsService> logger;
    private readonly ILykosAuthApi lykosAuthApi;
    private readonly ICivitTRPCApi civitTRPCApi;

    /// <inheritdoc />
    public event EventHandler<LykosAccountStatusUpdateEventArgs>? LykosAccountStatusUpdate;

    public LykosAccountStatusUpdateEventArgs? LykosStatus { get; private set; }

    public AccountsService(
        ILogger<AccountsService> logger,
        ILykosAuthApi lykosAuthApi,
        ICivitTRPCApi civitTRPCApi
    )
    {
        this.logger = logger;
        this.lykosAuthApi = lykosAuthApi;
        this.civitTRPCApi = civitTRPCApi;

        // Update our own status when the Lykos account status changes
        LykosAccountStatusUpdate += (_, args) => LykosStatus = args;
    }

    public async Task LykosLoginAsync(string email, string password)
    {
        var secrets = GlobalUserSecrets.LoadFromFile();

        var loginResponse = await lykosAuthApi.PostLogin(new PostLoginRequest(email, password));

        secrets.LykosAccessToken = loginResponse.AccessToken;
        secrets.LykosRefreshToken = loginResponse.RefreshToken;
        secrets.SaveToFile();

        await RefreshAsync();
    }

    public Task LykosLogoutAsync()
    {
        var secrets = GlobalUserSecrets.LoadFromFile();

        secrets.LykosAccessToken = null;
        secrets.LykosRefreshToken = null;
        secrets.SaveToFile();

        OnLykosAccountStatusUpdate(LykosAccountStatusUpdateEventArgs.Disconnected);

        return Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        var secrets = GlobalUserSecrets.LoadFromFile();

        await RefreshLykosAsync(secrets);
    }

    private async Task RefreshLykosAsync(GlobalUserSecrets secrets)
    {
        if (secrets.LykosAccessToken is { } accessToken && !string.IsNullOrEmpty(accessToken))
        {
            try
            {
                var user = await lykosAuthApi.GetUser("dev@ionite.io");

                OnLykosAccountStatusUpdate(
                    new LykosAccountStatusUpdateEventArgs { IsConnected = true, User = user }
                );

                return;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Timed out");
            }
            catch (ApiException e)
            {
                logger.LogWarning(e, "Failed to get user info from Lykos");
            }
        }

        OnLykosAccountStatusUpdate(LykosAccountStatusUpdateEventArgs.Disconnected);
    }

    private void OnLykosAccountStatusUpdate(LykosAccountStatusUpdateEventArgs e) =>
        LykosAccountStatusUpdate?.Invoke(this, e);
}
