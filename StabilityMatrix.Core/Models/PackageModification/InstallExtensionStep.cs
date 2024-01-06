using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class InstallExtensionStep(
    IPackageExtensionManager extensionManager,
    InstalledPackage installedPackage,
    PackageExtension packageExtension,
    PackageExtensionVersion? extensionVersion = null
) : IPackageStep
{
    public Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        return extensionManager.InstallExtensionAsync(
            packageExtension,
            installedPackage,
            extensionVersion,
            progress
        );
    }

    public string ProgressTitle => $"Installing Extension {packageExtension.Title}";
}
