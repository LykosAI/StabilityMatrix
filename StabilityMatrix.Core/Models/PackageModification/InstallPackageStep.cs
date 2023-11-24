using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallPackageStep : IPackageStep
{
    private readonly BasePackage package;
    private readonly TorchVersion torchVersion;
    private readonly SharedFolderMethod selectedSharedFolderMethod;
    private readonly DownloadPackageVersionOptions versionOptions;
    private readonly string installPath;

    public InstallPackageStep(
        BasePackage package,
        TorchVersion torchVersion,
        SharedFolderMethod selectedSharedFolderMethod,
        DownloadPackageVersionOptions versionOptions,
        string installPath
    )
    {
        this.package = package;
        this.torchVersion = torchVersion;
        this.selectedSharedFolderMethod = selectedSharedFolderMethod;
        this.versionOptions = versionOptions;
        this.installPath = installPath;
    }

    public async Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        void OnConsoleOutput(ProcessOutput output)
        {
            progress?.Report(new ProgressReport { IsIndeterminate = true, Message = output.Text });
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
