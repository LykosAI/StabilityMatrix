using System;
using System.Threading.Tasks;
using StabilityMatrix.Models;

namespace StabilityMatrix.Services;

public interface IDownloadService
{
    event EventHandler<ProgressReport>? DownloadProgressChanged;
    event EventHandler<ProgressReport>? DownloadComplete;
    Task DownloadToFileAsync(string downloadUrl, string downloadLocation, int bufferSize = ushort.MaxValue);
}
