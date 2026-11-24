using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.PackageModification;

public class ScanMetadataStep(
    DirectoryPath directoryPath,
    IMetadataImportService metadataImportService,
    bool updateExistingMetadata = false
) : IPackageStep
{
    /// <summary>Outcome tally of the scan, available after <see cref="ExecuteAsync"/> completes.</summary>
    public MetadataScanResult? Result { get; private set; }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        Result = updateExistingMetadata
            ? await metadataImportService
                .UpdateExistingMetadata(directoryPath, progress)
                .ConfigureAwait(false)
            : await metadataImportService
                .ScanDirectoryForMissingInfo(directoryPath, progress)
                .ConfigureAwait(false);
    }

    public string ProgressTitle => "Updating Metadata";
}
