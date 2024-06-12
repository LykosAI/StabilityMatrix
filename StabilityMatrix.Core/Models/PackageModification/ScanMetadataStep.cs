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
    public Task ExecuteAsync(IProgress<ProgressReport>? progress = null) =>
        updateExistingMetadata
            ? metadataImportService.UpdateExistingMetadata(directoryPath, progress)
            : metadataImportService.ScanDirectoryForMissingInfo(directoryPath, progress);

    public string ProgressTitle => "Updating Metadata";
}
