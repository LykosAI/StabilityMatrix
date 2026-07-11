using Injectio.Attributes;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Core.Api.Handlers;

[RegisterTransient]
public class RequestLoggingHandler(ILogger<RequestLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Refit Request URL: {RequestUri} ({RequestMethod})",
            request.RequestUri,
            request.Method
        );
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
