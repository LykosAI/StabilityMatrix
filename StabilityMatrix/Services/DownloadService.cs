using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly.Contrib.WaitAndRetry;
using StabilityMatrix.Models;

namespace StabilityMatrix.Services;

public class DownloadService : IDownloadService
{
    private readonly ILogger<DownloadService> logger;
    private readonly IHttpClientFactory httpClientFactory;

    public DownloadService(ILogger<DownloadService> logger, IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
    }

    public async Task DownloadToFileAsync(string downloadUrl, string downloadLocation, int bufferSize = ushort.MaxValue,
        IProgress<ProgressReport>? progress = null)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "1.0"));
        await using var file = new FileStream(downloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);
        
        long contentLength = 0;

        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        contentLength = response.Content.Headers.ContentLength ?? 0;
        
        var delays = Backoff.DecorrelatedJitterBackoffV2(
            TimeSpan.FromMilliseconds(50), retryCount: 3);
        
        foreach (var delay in delays)
        {
            if (contentLength > 0) break;
            logger.LogDebug("Retrying get-headers for content-length");
            await Task.Delay(delay);
            response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            contentLength = response.Content.Headers.ContentLength ?? 0;
        }
        var isIndeterminate = contentLength == 0;

        await using var stream = await response.Content.ReadAsStreamAsync();
        var totalBytesRead = 0L;
        var buffer = new byte[bufferSize];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;
            await file.WriteAsync(buffer.AsMemory(0, bytesRead));

            totalBytesRead += bytesRead;

            if (isIndeterminate)
            {
                progress?.Report(new ProgressReport(-1, isIndeterminate: true));
            }
            else
            {
                progress?.Report(new ProgressReport(current: Convert.ToUInt64(totalBytesRead),
                    total: Convert.ToUInt64(contentLength), message: "Downloading..."));
            }
        }

        await file.FlushAsync();
        
        progress?.Report(new ProgressReport(1f, message: "Download complete!"));
    }
}
