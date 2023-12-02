using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Services;

public interface IMetadataImportService
{
    Task ScanDirectoryForMissingInfo(
        DirectoryPath directory,
        IProgress<ProgressReport>? progress = null
    );

    Task<ConnectedModelInfo?> GetMetadataForFile(
        FilePath filePath,
        IProgress<ProgressReport>? progress = null,
        bool forceReimport = false
    );

    Task UpdateExistingMetadata(
        DirectoryPath directory,
        IProgress<ProgressReport>? progress = null
    );
}
