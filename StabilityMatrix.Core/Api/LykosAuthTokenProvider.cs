using Injectio.Attributes;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Api;

[RegisterSingleton<LykosAuthTokenProvider>]
public class LykosAuthTokenProvider : ITokenProvider
{
    private readonly ISecretsManager secretsManager;
    private readonly Lazy<ILykosAuthApi> lazyLykosAuthApi;

    public LykosAuthTokenProvider(Lazy<ILykosAuthApi> lazyLykosAuthApi, ISecretsManager secretsManager)
    {
        // Lazy as instantiating requires the current class to be instantiated.
        this.lazyLykosAuthApi = lazyLykosAuthApi;
        this.secretsManager = secretsManager;
    }

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);

        return secrets.LykosAccount?.AccessToken ?? "";
    }

    /// <inheritdoc />
    public async Task<(string AccessToken, string RefreshToken)> RefreshTokensAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(secrets.LykosAccount?.RefreshToken))
        {
            throw new InvalidOperationException("No refresh token found");
        }

        var lykosAuthApi = lazyLykosAuthApi.Value;
        var newTokens = await lykosAuthApi
            .PostLoginRefresh(new PostLoginRefreshRequest(secrets.LykosAccount.RefreshToken))
            .ConfigureAwait(false);

        secrets = secrets with { LykosAccount = newTokens };

        await secretsManager.SaveAsync(secrets).ConfigureAwait(false);

        return (newTokens.AccessToken, newTokens.RefreshToken);
    }
}
