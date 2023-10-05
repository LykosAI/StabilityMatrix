using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockDownloadService : IDownloadService
{
    public Task DownloadToFileAsync(
        string downloadUrl,
        string downloadPath,
        IProgress<ProgressReport>? progress = null,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeDownloadToFileAsync(
        string downloadUrl,
        string downloadPath,
        long existingFileSize,
        IProgress<ProgressReport>? progress = null,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<long> GetFileSizeAsync(
        string downloadUrl,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(0L);
    }

    public Task<Stream?> GetImageStreamFromUrl(string url)
    {
        return Task.FromResult(new MemoryStream(new byte[24]) as Stream)!;
    }
}
