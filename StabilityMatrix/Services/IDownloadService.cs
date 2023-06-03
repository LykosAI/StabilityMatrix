using System;
using System.Threading.Tasks;

namespace StabilityMatrix.Services;

public interface IDownloadService
{
    event EventHandler<int>? DownloadProgressChanged;
    event EventHandler<string>? DownloadComplete;
    Task DownloadToFileAsync(string downloadUrl, string downloadLocation, ushort bufferSize = ushort.MaxValue);
}
