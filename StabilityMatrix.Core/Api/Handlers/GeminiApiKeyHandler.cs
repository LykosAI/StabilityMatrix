using System.Net.Http.Headers;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Api.Handlers;

/// <summary>
/// HTTP message handler that adds Gemini API key to requests
/// </summary>
public class GeminiApiKeyHandler(ISecretsManager secretsManager) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);

        if (!string.IsNullOrEmpty(secrets.GeminiApiKey))
        {
            request.Headers.Add("x-goog-api-key", secrets.GeminiApiKey);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
