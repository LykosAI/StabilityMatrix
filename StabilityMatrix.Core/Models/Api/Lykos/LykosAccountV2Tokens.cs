using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace StabilityMatrix.Core.Models.Api.Lykos;

public record LykosAccountV2Tokens(string AccessToken, string? RefreshToken, string? IdentityToken)
{
    public JsonWebToken? GetDecodedIdentityToken()
    {
        if (string.IsNullOrWhiteSpace(IdentityToken))
        {
            return null;
        }

        var handler = new JsonWebTokenHandler();
        return handler.ReadJsonWebToken(IdentityToken);
    }

    public ClaimsPrincipal? GetIdentityTokenPrincipal()
    {
        if (GetDecodedIdentityToken() is not { } token)
        {
            return null;
        }

        return new ClaimsPrincipal(new ClaimsIdentity(token.Claims, "IdentityToken"));
    }

    public DateTimeOffset? GetIdentityTokenExpiration()
    {
        if (GetDecodedIdentityToken() is not { } token)
        {
            return null;
        }

        return token.ValidTo;
    }
}
