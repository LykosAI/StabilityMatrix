using System;
using System.Threading.Tasks;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Progress;

namespace StabilityMatrix.Services;

public interface IDownloadService
{
    Task DownloadToFileAsync(string downloadUrl, string downloadPath,
        IProgress<ProgressReport>? progress = null, string? httpClientName = null);
}
