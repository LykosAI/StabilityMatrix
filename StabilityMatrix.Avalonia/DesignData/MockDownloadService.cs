using System;
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
}
