using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallPackageStep(
    BasePackage package,
    TorchVersion torchVersion,
    SharedFolderMethod selectedSharedFolderMethod,
    DownloadPackageVersionOptions versionOptions,
    string installPath
) : IPackageStep
{
    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        void OnConsoleOutput(ProcessOutput output)
        {
            progress?.Report(new ProgressReport(-1f, isIndeterminate: true) { ProcessOutput = output });
        }

        await package
            .InstallPackage(
                installPath,
                torchVersion,
                selectedSharedFolderMethod,
                versionOptions,
                progress,
                OnConsoleOutput
            )
            .ConfigureAwait(false);
    }

    public string ProgressTitle => $"Installing {package.DisplayName}...";
}
