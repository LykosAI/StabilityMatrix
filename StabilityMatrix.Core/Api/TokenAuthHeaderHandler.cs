using System.Net;
using System.Net.Http.Headers;
using NLog;
using Polly;
using Polly.Retry;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Api;

public class TokenAuthHeaderHandler : DelegatingHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly AsyncRetryPolicy<HttpResponseMessage> policy;
    private readonly ITokenProvider tokenProvider;

    public Func<HttpRequestMessage, bool> RequestFilter { get; set; } =
        request => request.Headers.Authorization is { Scheme: "Bearer" };

    public Func<HttpResponseMessage, bool> ResponseFilter { get; set; } =
        response =>
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            && response.RequestMessage?.Headers.Authorization is { Scheme: "Bearer", Parameter: { } param }
            && !string.IsNullOrWhiteSpace(param);

    public TokenAuthHeaderHandler(ITokenProvider tokenProvider)
    {
        this.tokenProvider = tokenProvider;

        policy = Policy
            .HandleResult(ResponseFilter)
            .RetryAsync(
                async (result, _) =>
                {
                    var oldToken = ObjectHash.GetStringSignature(
                        await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false)
                    );
                    Logger.Info(
                        "Refreshing access token for status ({StatusCode})",
                        result.Result.StatusCode
                    );
                    var (newToken, _) = await tokenProvider.RefreshTokensAsync().ConfigureAwait(false);

                    Logger.Info(
                        "Access token refreshed: {OldToken} -> {NewToken}",
                        ObjectHash.GetStringSignature(oldToken),
                        ObjectHash.GetStringSignature(newToken)
                    );
                }
            );
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        return policy.ExecuteAsync(async () =>
        {
            // Only add if Authorization is already set to Bearer and access token is not empty
            // this allows some routes to not use the access token
            if (RequestFilter(request))
            {
                var accessToken = await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        });
    }
}
