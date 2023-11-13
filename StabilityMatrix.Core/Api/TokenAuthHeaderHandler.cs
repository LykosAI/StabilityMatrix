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

    public TokenAuthHeaderHandler(ITokenProvider tokenProvider)
    {
        this.tokenProvider = tokenProvider;

        policy = Policy
            .HandleResult<HttpResponseMessage>(
                r =>
                    r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    && r.RequestMessage?.Headers.Authorization
                        is { Parameter: "Bearer", Scheme: not null }
            )
            .RetryAsync(
                async (result, _) =>
                {
                    var oldToken = ObjectHash.GetStringSignature(
                        await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false)
                    );
                    Logger.Info(
                        "Refreshing access token for status ({StatusCode}) {Message}",
                        result.Result.StatusCode,
                        result.Exception.Message
                    );
                    var (newToken, _) = await tokenProvider
                        .RefreshTokensAsync()
                        .ConfigureAwait(false);

                    Logger.Info(
                        "Access token refreshed: {OldToken} -> {NewToken}",
                        ObjectHash.GetStringSignature(oldToken),
                        ObjectHash.GetStringSignature(newToken)
                    );
                }
            );

        // InnerHandler must be left as null when using DI, but must be assigned a value when
        // using RestService.For<IMyApi>
        // InnerHandler = new HttpClientHandler();
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        return policy.ExecuteAsync(async () =>
        {
            var accessToken = await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        });
    }
}
