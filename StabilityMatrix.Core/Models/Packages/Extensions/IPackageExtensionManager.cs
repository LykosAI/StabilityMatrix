using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

/// <summary>
/// Interface for a package extension manager.
/// </summary>
public interface IPackageExtensionManager
{
    IEnumerable<ExtensionManifest> DefaultManifests { get; }

    IEnumerable<ExtensionManifest> GetManifests(InstalledPackage installedPackage)
    {
        return DefaultManifests;
    }

    Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
        ExtensionManifest manifest,
        CancellationToken cancellationToken = default
    );

    async Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
        IEnumerable<ExtensionManifest> manifests,
        CancellationToken cancellationToken = default
    )
    {
        var extensions = Enumerable.Empty<PackageExtension>();

        foreach (var manifest in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            extensions = extensions.Concat(
                await GetManifestExtensionsAsync(manifest, cancellationToken).ConfigureAwait(false)
            );
        }

        return extensions;
    }

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
