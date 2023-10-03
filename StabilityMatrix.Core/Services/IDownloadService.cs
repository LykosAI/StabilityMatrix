using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Services;

public interface IDownloadService
{
    Task DownloadToFileAsync(
        string downloadUrl,
        string downloadPath,
        IProgress<ProgressReport>? progress = null,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    );

    Task ResumeDownloadToFileAsync(
        string downloadUrl,
        string downloadPath,
        long existingFileSize,
        IProgress<ProgressReport>? progress = null,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    );

    Task<long> GetFileSizeAsync(
        string downloadUrl,
        string? httpClientName = null,
        CancellationToken cancellationToken = default
    );

    Task<Stream?> GetImageStreamFromUrl(string url);
}
