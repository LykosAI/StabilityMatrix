using System.Net;
using Refit;
using StabilityMatrix.Core.Models.Api.Lykos;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface ILykosAuthApi
{
    [Headers("Authorization: Bearer")]
    [Get("/api/Users/{email}")]
    Task<GetUserResponse> GetUser(string email, CancellationToken cancellationToken = default);

    [Headers("Authorization: Bearer")]
    [Get("/api/Users/me")]
    Task<GetUserResponse> GetUserSelf(CancellationToken cancellationToken = default);

    [Post("/api/Accounts")]
    Task<LykosAccountTokens> PostAccount(
        [Body] PostAccountRequest request,
        CancellationToken cancellationToken = default
    );

    [Post("/api/Login")]
    Task<LykosAccountTokens> PostLogin(
        [Body] PostLoginRequest request,
        CancellationToken cancellationToken = default
    );

    [Post("/api/Login/Refresh")]
    Task<LykosAccountTokens> PostLoginRefresh(
        [Body] PostLoginRefreshRequest request,
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
        var result = await GetPatreonOAuthRedirect(redirectUrl, cancellationToken)
            .ConfigureAwait(false);

        if (result.StatusCode != HttpStatusCode.Redirect)
        {
            result.EnsureSuccessStatusCode();
            throw new InvalidOperationException(
                $"Expected a redirect 302 response, got {result.StatusCode}"
            );
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
}
