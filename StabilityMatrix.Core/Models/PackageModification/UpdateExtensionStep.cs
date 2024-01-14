using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class UpdateExtensionStep(
    IPackageExtensionManager extensionManager,
    InstalledPackage installedPackage,
    InstalledPackageExtension installedExtension,
    PackageExtensionVersion? extensionVersion = null
) : IPackageStep
{
    public Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        return extensionManager.UpdateExtensionAsync(
            installedExtension,
            installedPackage,
            extensionVersion,
            progress
        );
    }

    public string ProgressTitle => $"Updating Extension {installedExtension.Title}";
}
