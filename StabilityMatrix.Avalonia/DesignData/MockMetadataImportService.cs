using System;
using System.Threading.Tasks;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockMetadataImportService : IMetadataImportService
{
    public Task ScanDirectoryForMissingInfo(DirectoryPath directory, IProgress<ProgressReport>? progress = null)
    {
        return Task.CompletedTask;
    }

    public Task<ConnectedModelInfo?> GetMetadataForFile(
        FilePath filePath,
        IProgress<ProgressReport>? progress = null,
        bool forceReimport = false
    )
    {
        return null;
    }

    public Task UpdateExistingMetadata(DirectoryPath directory, IProgress<ProgressReport>? progress = null)
    {
        return Task.CompletedTask;
    }
}
