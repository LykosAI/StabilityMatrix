using System;
using System.Collections.Generic;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockTrackedDownloadService : ITrackedDownloadService
{
    /// <inheritdoc />
    public IEnumerable<TrackedDownload> Downloads => Array.Empty<TrackedDownload>();
    
    /// <inheritdoc />
    public event EventHandler<TrackedDownload>? DownloadAdded;

    /// <inheritdoc />
    public TrackedDownload NewDownload(Uri downloadUrl, FilePath downloadPath)
    {
        throw new NotImplementedException();
    }
}
