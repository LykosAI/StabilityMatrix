using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using ApiException = Refit.ApiException;

namespace StabilityMatrix.Avalonia.Services;

[Singleton(typeof(IAccountsService))]
public class AccountsService : IAccountsService
{
    private readonly ILogger<AccountsService> logger;
    private readonly ILykosAuthApi lykosAuthApi;
    private readonly ICivitTRPCApi civitTRPCApi;

    public bool IsLykosConnected { get; private set; }

    public AccountsService(
        ILogger<AccountsService> logger,
        ILykosAuthApi lykosAuthApi,
        ICivitTRPCApi civitTRPCApi
    )
    {
        this.logger = logger;
        this.lykosAuthApi = lykosAuthApi;
        this.civitTRPCApi = civitTRPCApi;
    }

    public async Task LykosLoginAsync(string email, string password)
    {
        var secrets = GlobalUserSecrets.LoadFromFile();

        var loginResponse = await lykosAuthApi.PostLogin(new PostLoginRequest(email, password));

        secrets.LykosAccessToken = loginResponse.AccessToken;
        secrets.LykosRefreshToken = loginResponse.RefreshToken;
        secrets.SaveToFile();

        IsLykosConnected = true;
    }

    public Task LykosLogoutAsync()
    {
        var secrets = GlobalUserSecrets.LoadFromFile();

        secrets.LykosAccessToken = null;
        secrets.LykosRefreshToken = null;
        secrets.SaveToFile();

        IsLykosConnected = false;

        return Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        var secrets = GlobalUserSecrets.LoadFromFile();

        IsLykosConnected = false;

        if (secrets.LykosAccessToken is { } accessToken && !string.IsNullOrEmpty(accessToken))
        {
            try
            {
                var user = await lykosAuthApi.GetUser("dev@ionite.io");

                IsLykosConnected = true;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Timed out");
            }
            catch (ApiException e)
            {
                logger.LogError(e, "Failed to get user info from Lykos");
            }
        }
    }
}
