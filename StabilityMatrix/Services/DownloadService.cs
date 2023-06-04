using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    
    public event EventHandler<ProgressReport>? DownloadProgressChanged;
    public event EventHandler<ProgressReport>? DownloadComplete;
    
    public async Task DownloadToFileAsync(string downloadUrl, string downloadLocation, int bufferSize = ushort.MaxValue)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StabilityMatrix", "1.0"));
        await using var file = new FileStream(downloadLocation, FileMode.Create, FileAccess.Write, FileShare.None);
        
        long contentLength = 0;
        var retryCount = 0;
        
        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        contentLength = response.Content.Headers.ContentLength ?? 0;
        
        while (contentLength == 0 && retryCount++ < 5)
        {
            logger.LogDebug("Retrying get-headers for content-length");
            Thread.Sleep(50);
            response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            contentLength = response.Content.Headers.ContentLength ?? 0;
        }

        var isIndeterminate = contentLength == 0;

        await using var stream = await response.Content.ReadAsStreamAsync();
        var totalBytesRead = 0;
        while (true)
        {
            var buffer = new byte[bufferSize];
            var bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;
            await file.WriteAsync(buffer.AsMemory(0, bytesRead));

            totalBytesRead += bytesRead;

            if (isIndeterminate)
            {
                OnDownloadProgressChanged(-1);
            }
            else
            {
                var progress = totalBytesRead / (double) contentLength;
                OnDownloadProgressChanged(progress);
            }
        }

        await file.FlushAsync();
        OnDownloadComplete(downloadLocation);
    }

    private void OnDownloadProgressChanged(double progress) =>
        DownloadProgressChanged?.Invoke(this, new ProgressReport(progress));

    private void OnDownloadComplete(string path) =>
        DownloadComplete?.Invoke(this, new ProgressReport(progress: 100f, message: path));
}
