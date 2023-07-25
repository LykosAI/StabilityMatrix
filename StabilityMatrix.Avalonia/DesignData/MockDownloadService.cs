using System;
using System.IO;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockDownloadService : IDownloadService
{
    public Task DownloadToFileAsync(string downloadUrl, string downloadPath,
        IProgress<ProgressReport>? progress = null, string? httpClientName = null)
    {
        return Task.CompletedTask;
    }

    public Task<Stream> GetImageStreamFromUrl(string url)
    {
        return Task.FromResult(new MemoryStream(new byte[24]) as Stream);
    }
}
