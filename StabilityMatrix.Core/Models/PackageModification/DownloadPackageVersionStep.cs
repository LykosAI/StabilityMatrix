using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class DownloadPackageVersionStep(
    BasePackage package,
    string installPath,
    DownloadPackageOptions options
) : ICancellablePackageStep
{
    public Task ExecuteAsync(
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        return package.DownloadPackage(installPath, options, progress, cancellationToken);
    }

    public string ProgressTitle => "Downloading package...";
}
