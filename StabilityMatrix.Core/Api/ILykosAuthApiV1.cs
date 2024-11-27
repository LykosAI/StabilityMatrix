using System.ComponentModel;
using System.Net;
using Refit;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Api;

[Localizable(false)]
[Headers("User-Agent: StabilityMatrix")]
[Obsolete("Use ILykosAuthApiV2")]
public interface ILykosAuthApiV1
{
    [Headers("Authorization: Bearer")]
    [Get("/api/Users/{email}")]
    Task<GetUserResponse> GetUser(string email, CancellationToken cancellationToken = default);

    [Headers("Authorization: Bearer")]
    [Get("/api/Users/me")]
    Task<GetUserResponse> GetUserSelf(CancellationToken cancellationToken = default);

    [Post("/api/Accounts")]
    Task<LykosAccountV1Tokens> PostAccount(
        [Body] PostAccountRequest request,
        CancellationToken cancellationToken = default
    );

    [Post("/api/Login")]
    Task<LykosAccountV1Tokens> PostLogin(
        [Body] PostLoginRequest request,
        CancellationToken cancellationToken = default
    );

    [Headers("Authorization: Bearer")]
    [Post("/api/Login/Refresh")]
    Task<LykosAccountV1Tokens> PostLoginRefresh(
        [Body] PostLoginRefreshRequest request,
        CancellationToken cancellationToken = default
    );

    [Get("/api/oauth/google/callback")]
    Task<LykosAccountV1Tokens> GetOAuthGoogleCallback(
        [Query] string code,
        [Query] string state,
        [Query] string codeVerifier,
        CancellationToken cancellationToken = default
    );

    [Get("/api/oauth/google/links/login-or-signup")]
    Task<Uri> GetOAuthGoogleLoginOrSignupLink(
        string redirectUri,
        string codeChallenge,
        string codeChallengeMethod,
        CancellationToken cancellationToken = default
    );

    [Headers("Authorization: Bearer")]
    [Get("/api/oauth/patreon/redirect")]
    Task<HttpResponseMessage> GetPatreonOAuthRedirect(
        string redirectUrl,
        CancellationToken cancellationToken = default
    );

    public async Task<string> GetPatreonOAuthUrl(
        string redirectUrl,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GetPatreonOAuthRedirect(redirectUrl, cancellationToken).ConfigureAwait(false);

        if (result.StatusCode != HttpStatusCode.Redirect)
        {
            result.EnsureSuccessStatusCode();
            throw new InvalidOperationException($"Expected a redirect 302 response, got {result.StatusCode}");
        }

        return result.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Expected a redirect URL, but got none");
    }

    [Headers("Authorization: Bearer")]
    [Delete("/api/oauth/patreon")]
    Task DeletePatreonOAuth(CancellationToken cancellationToken = default);

    [Headers("Authorization: Bearer")]
    [Get("/api/files/download")]
    Task<GetFilesDownloadResponse> GetFilesDownload(
        string path,
        CancellationToken cancellationToken = default
    );

    [Get("/api/Models/recommended")]
    Task<CivitModelsResponse> GetRecommendedModels();
}
