using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class DownloadPackageVersionStep : IPackageStep
{
    private readonly BasePackage package;
    private readonly string installPath;
    private readonly DownloadPackageVersionOptions downloadOptions;

    public DownloadPackageVersionStep(
        BasePackage package,
        string installPath,
        DownloadPackageVersionOptions downloadOptions
    )
    {
        this.package = package;
        this.installPath = installPath;
        this.downloadOptions = downloadOptions;
    }

    public Task ExecuteAsync(IProgress<ProgressReport>? progress = null) =>
        package.DownloadPackage(installPath, downloadOptions, progress);

    public string ProgressTitle => "Downloading package...";
}
