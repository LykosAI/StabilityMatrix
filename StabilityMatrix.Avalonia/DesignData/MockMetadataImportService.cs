using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockMetadataImportService : IMetadataImportService
{
    public Task<MetadataScanResult> ScanDirectoryForMissingInfo(
        DirectoryPath directory,
        IProgress<ProgressReport>? progress = null
    )
    {
        return Task.FromResult(new MetadataScanResult { Total = 0 });
    }

    public Task<MetadataFetchResult> GetMetadataForFile(
        FilePath filePath,
        IProgress<ProgressReport>? progress = null,
        bool forceReimport = false
    )
    {
        return Task.FromResult(new MetadataFetchResult(MetadataFetchStatus.NotFound, null));
    }

    public Task<MetadataScanResult> UpdateExistingMetadata(
        DirectoryPath directory,
        IProgress<ProgressReport>? progress = null
    )
    {
        return Task.FromResult(new MetadataScanResult { Total = 0 });
    }
}
