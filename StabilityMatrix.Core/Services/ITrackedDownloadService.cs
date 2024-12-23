using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Core.Services;

public interface ITrackedDownloadService
{
    IEnumerable<TrackedDownload> Downloads { get; }

    event EventHandler<TrackedDownload>? DownloadAdded;

    TrackedDownload NewDownload(Uri downloadUrl, FilePath downloadPath);

    TrackedDownload NewDownload(string downloadUrl, FilePath downloadPath) =>
        NewDownload(new Uri(downloadUrl), downloadPath);
    Task TryStartDownload(TrackedDownload download);
    Task TryResumeDownload(TrackedDownload download);
    void UpdateMaxConcurrentDownloads(int newMax);
}
