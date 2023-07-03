using System;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Services;

public interface IDownloadService
{
    Task DownloadToFileAsync(string downloadUrl, string downloadLocation,
        IProgress<ProgressReport>? progress = null, string? httpClientName = null);
}
