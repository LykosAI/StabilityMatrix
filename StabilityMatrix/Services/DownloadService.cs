using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.Services;

public class DownloadService : IDownloadService
{
    private readonly ILogger<DownloadService> logger;

    public DownloadService(ILogger<DownloadService> logger)
    {
        this.logger = logger;
    }
    
    public event EventHandler<int>? DownloadProgressChanged;
    public event EventHandler<string>? DownloadComplete;
    
    public async Task DownloadToFileAsync(string downloadUrl, string downloadLocation, ushort bufferSize = ushort.MaxValue)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
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
                var progress = (int)(totalBytesRead * 100d / contentLength);
                logger.LogDebug($"Progress; {progress}");
                
                OnDownloadProgressChanged(progress);
            }
        }

        await file.FlushAsync();
        OnDownloadComplete(downloadLocation);
    }
    
    private void OnDownloadProgressChanged(int progress) => DownloadProgressChanged?.Invoke(this, progress);
    private void OnDownloadComplete(string path) => DownloadComplete?.Invoke(this, path);
}
