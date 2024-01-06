using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

/// <summary>
/// Interface for a package extension manager.
/// </summary>
public interface IPackageExtensionManager
{
    IEnumerable<ExtensionManifest> GetManifests(InstalledPackage installedPackage);

    Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
        ExtensionManifest manifest,
        CancellationToken cancellationToken = default
    );

    Task<IEnumerable<InstalledPackageExtension>> GetInstalledExtensionsAsync(
        InstalledPackage installedPackage,
        CancellationToken cancellationToken = default
    );

    Task InstallExtensionAsync(
        PackageExtension extension,
        InstalledPackage installedPackage,
        PackageExtensionVersion? version = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );

    Task UninstallExtensionAsync(
        InstalledPackageExtension installedExtension,
        InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
}
