using OpenIddict.Client;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Lykos;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Api;

[Singleton]
public class LykosAuthTokenProvider(ISecretsManager secretsManager, OpenIddictClientService openIdClient)
    : ITokenProvider
{
    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);

        return secrets.LykosAccountV2?.AccessToken ?? "";
    }

    /// <inheritdoc />
    public async Task<(string AccessToken, string RefreshToken)> RefreshTokensAsync()
    {
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(secrets.LykosAccountV2?.RefreshToken))
        {
            throw new InvalidOperationException("No refresh token found");
        }

        var result = await openIdClient
            .AuthenticateWithRefreshTokenAsync(
                new OpenIddictClientModels.RefreshTokenAuthenticationRequest
                {
                    ProviderName = OpenIdClientConstants.LykosAccount.ProviderName,
                    RefreshToken = secrets.LykosAccountV2.RefreshToken
                }
            )
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(result.RefreshToken))
        {
            throw new InvalidOperationException("No refresh token returned");
        }

        secrets = secrets with
        {
            LykosAccountV2 = new LykosAccountV2Tokens(
                result.AccessToken,
                result.RefreshToken,
                result.IdentityToken
            )
        };

        await secretsManager.SaveAsync(secrets).ConfigureAwait(false);

        return (result.AccessToken, result.RefreshToken);
    }
}
