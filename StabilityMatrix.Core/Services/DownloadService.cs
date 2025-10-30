using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Polly.Contrib.WaitAndRetry;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<IDownloadService, DownloadService>]
public class DownloadService : IDownloadService
{
    private readonly ILogger<DownloadService> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ISecretsManager secretsManager;
    private const int BufferSize = ushort.MaxValue;

    public DownloadService(
        ILogger<DownloadService> logger,
        IHttpClientFactory httpClientFactory,
        ISecretsManager secretsManager
    )
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.secretsManager = secretsManager;
    }

    public async Task DownloadToFileAsync(
        string downloadUrl,
        string downloadPath,
        IProgress<ProgressReport>? progress = null,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    )
    {
        using var client = string.IsNullOrWhiteSpace(httpClientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(httpClientName);

        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "2.0"));

        await AddConditionalHeaders(client, new Uri(downloadUrl)).ConfigureAwait(false);

        await using var file = new FileStream(
            downloadPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );

        long contentLength = 0;

        var response = await client
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        contentLength = response.Content.Headers.ContentLength ?? 0;

        var delays = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(50), retryCount: 3);

        foreach (var delay in delays)
        {
            if (contentLength > 0)
                break;
            logger.LogDebug("Retrying get-headers for content-length");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            response = await client
                .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            contentLength = response.Content.Headers.ContentLength ?? 0;
        }
        var isIndeterminate = contentLength == 0;

        if (contentLength > 0)
        {
            // check free space
            if (
                SystemInfo.GetDiskFreeSpaceBytes(Path.GetDirectoryName(downloadPath)) is { } freeSpace
                && freeSpace < contentLength
            )
            {
                throw new ApplicationException(
                    $"Not enough free space to download file. Free: {freeSpace} bytes, Required: {contentLength} bytes"
                );
            }
        }

        await using var stream = await response
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        var totalBytesRead = 0L;
        var buffer = new byte[BufferSize];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                break;
            await file.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

            totalBytesRead += bytesRead;

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            var speedInMBps = (totalBytesRead / elapsedSeconds) / (1024 * 1024);

            if (isIndeterminate)
            {
                progress?.Report(new ProgressReport(-1, isIndeterminate: true));
            }
            else
            {
                progress?.Report(
                    new ProgressReport(
                        current: Convert.ToUInt64(totalBytesRead),
                        total: Convert.ToUInt64(contentLength),
                        message: "Downloading...",
                        printToConsole: false,
                        speedInMBps: speedInMBps
                    )
                );
            }
        }

        await file.FlushAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, message: "Download complete!"));
    }

    /// <inheritdoc />
    public async Task ResumeDownloadToFileAsync(
        string downloadUrl,
        string downloadPath,
        long existingFileSize,
        IProgress<ProgressReport>? progress = null,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    )
    {
        using var client = string.IsNullOrWhiteSpace(httpClientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(httpClientName);

        using var noRedirectClient = httpClientFactory.CreateClient("DontFollowRedirects");

        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "2.0"));

        await AddConditionalHeaders(client, new Uri(downloadUrl)).ConfigureAwait(false);
        await AddConditionalHeaders(noRedirectClient, new Uri(downloadUrl)).ConfigureAwait(false);

        // Create file if it doesn't exist
        if (!File.Exists(downloadPath))
        {
            logger.LogInformation("Resume file doesn't exist, creating file {DownloadPath}", downloadPath);
            File.Create(downloadPath).Close();
        }

        await using var file = new FileStream(
            downloadPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.None
        );

        // Remaining content length
        long remainingContentLength = 0;
        // Total of the original content
        long originalContentLength = 0;

        using var noRedirectRequest = new HttpRequestMessage();
        noRedirectRequest.Method = HttpMethod.Get;
        noRedirectRequest.RequestUri = new Uri(downloadUrl);
        noRedirectRequest.Headers.Range = new RangeHeaderValue(existingFileSize, null);

        HttpResponseMessage? response = null;
        foreach (
            var delay in Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(50), retryCount: 4)
        )
        {
            var noRedirectResponse = await noRedirectClient
                .SendAsync(noRedirectRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if ((int)noRedirectResponse.StatusCode > 299 && (int)noRedirectResponse.StatusCode < 400)
            {
                var redirectUrl = noRedirectResponse.Headers.Location?.ToString();
                if (redirectUrl != null && redirectUrl.Contains("reason=download-auth"))
                {
                    throw new UnauthorizedAccessException();
                }
            }
            else if (noRedirectResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (
                    noRedirectRequest.RequestUri?.Host.Equals(
                        "huggingface.co",
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                )
                {
                    throw new HuggingFaceLoginRequiredException();
                }
                if (
                    noRedirectRequest.RequestUri?.Host.Equals(
                        "civitai.com",
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                )
                {
                    var responseContent = await noRedirectResponse
                        .Content.ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (responseContent.Contains("The creator of this asset has disabled downloads"))
                    {
                        throw new CivitDownloadDisabledException();
                    }

                    throw new CivitLoginRequiredException();
                }

                throw new UnauthorizedAccessException();
            }
            else if (noRedirectResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new EarlyAccessException();
            }

            using var redirectRequest = new HttpRequestMessage();
            redirectRequest.Method = HttpMethod.Get;
            redirectRequest.RequestUri = new Uri(downloadUrl);
            redirectRequest.Headers.Range = new RangeHeaderValue(existingFileSize, null);

            response = await client
                .SendAsync(redirectRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            remainingContentLength = response.Content.Headers.ContentLength ?? 0;
            originalContentLength = response.Content.Headers.ContentRange?.Length.GetValueOrDefault() ?? 0;

            if (remainingContentLength > 0)
                break;

            logger.LogDebug("Retrying get-headers for content-length");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        if (response == null)
        {
            throw new ApplicationException("Response is null");
        }

        var isIndeterminate = remainingContentLength == 0;

        await using var stream = await response
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var totalBytesRead = 0L;
        var stopwatch = Stopwatch.StartNew();
        var buffer = new byte[BufferSize];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                break;
            await file.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

            totalBytesRead += bytesRead;

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            var speedInMBps = (totalBytesRead / elapsedSeconds) / (1024 * 1024);

            if (isIndeterminate)
            {
                progress?.Report(new ProgressReport(-1, isIndeterminate: true));
            }
            else
            {
                progress?.Report(
                    new ProgressReport(
                        // Report the current as session current + original start size
                        current: Convert.ToUInt64(totalBytesRead + existingFileSize),
                        // Total as the original total
                        total: Convert.ToUInt64(originalContentLength),
                        message: "Downloading...",
                        speedInMBps: speedInMBps
                    )
                );
            }
        }

        await file.FlushAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, message: "Download complete!"));
    }

    /// <inheritdoc />
    public async Task<long> GetFileSizeAsync(
        string downloadUrl,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    )
    {
        using var client = string.IsNullOrWhiteSpace(httpClientName)
            ? httpClientFactory.CreateClient()
            : httpClientFactory.CreateClient(httpClientName);

        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "2.0"));

        await AddConditionalHeaders(client, new Uri(downloadUrl)).ConfigureAwait(false);

        var contentLength = 0L;

        foreach (
            var delay in Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(50), retryCount: 3)
        )
        {
            var response = await client
                .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            contentLength = response.Content.Headers.ContentLength ?? -1;

            if (contentLength > 0)
                break;

            logger.LogDebug("Retrying get-headers for content-length");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return contentLength;
    }

    public async Task<Stream?> GetImageStreamFromUrl(string url)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "2.0"));
        await AddConditionalHeaders(client, new Uri(url)).ConfigureAwait(false);
        try
        {
            var response = await client.GetAsync(url).ConfigureAwait(false);
            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get image stream from url {Url}", url);
            return null;
        }
    }

    public async Task<Stream> GetContentAsync(string url, CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "2.0"));

        await AddConditionalHeaders(client, new Uri(url)).ConfigureAwait(false);

        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds conditional headers to the HttpClient for the given URL
    /// </summary>
    private async Task AddConditionalHeaders(HttpClient client, Uri url)
    {
        // Check if civit download
        if (url.Host.Equals("civitai.com", StringComparison.OrdinalIgnoreCase))
        {
            // Add auth if we have it
            if (await secretsManager.SafeLoadAsync().ConfigureAwait(false) is { CivitApi: { } civitApi })
            {
                logger.LogTrace(
                    "Adding Civit auth header {Signature} for download {Url}",
                    ObjectHash.GetStringSignature(civitApi.ApiToken),
                    url
                );
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    civitApi.ApiToken
                );
            }
        }
        // Check if Hugging Face download
        else if (url.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase))
        {
            var secrets = await secretsManager.SafeLoadAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(secrets.HuggingFaceToken))
            {
                logger.LogTrace("Adding Hugging Face auth header for download {Url}", url);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    secrets.HuggingFaceToken
                );
            }
        }
    }
}
