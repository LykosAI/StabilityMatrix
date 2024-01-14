using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.PackageModification;

public class UninstallExtensionStep(
    IPackageExtensionManager extensionManager,
    InstalledPackage installedPackage,
    InstalledPackageExtension packageExtension
) : IPackageStep
{
    public Task ExecuteAsync(IProgress<ProgressReport>? progress = null)
    {
        return extensionManager.UninstallExtensionAsync(packageExtension, installedPackage, progress);
    }

    public string ProgressTitle => $"Uninstalling Extension {packageExtension.Title}";
}
