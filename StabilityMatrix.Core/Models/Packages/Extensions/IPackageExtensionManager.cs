using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

/// <summary>
/// Interface for a package extension manager.
/// </summary>
public interface IPackageExtensionManager
{
    /// <summary>
    /// Default manifests for this extension manager.
    /// </summary>
    IEnumerable<ExtensionManifest> DefaultManifests { get; }

    /// <summary>
    /// Get manifests given an installed package.
    /// By default returns <see cref="DefaultManifests"/>.
    /// </summary>
    IEnumerable<ExtensionManifest> GetManifests(InstalledPackage installedPackage)
    {
        return DefaultManifests;
    }

    /// <summary>
    /// Get extensions from the provided manifest.
    /// </summary>
    Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
        ExtensionManifest manifest,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get extensions from all provided manifests.
    /// </summary>
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

    /// <summary>
    /// Get unique extensions from all provided manifests. As a mapping of their reference.
    /// </summary>
    async Task<IDictionary<string, PackageExtension>> GetManifestExtensionsMapAsync(
        IEnumerable<ExtensionManifest> manifests,
        CancellationToken cancellationToken = default
    )
    {
        var result = new Dictionary<string, PackageExtension>();

        foreach (
            var extension in await GetManifestExtensionsAsync(manifests, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = extension.Reference.ToString();

            if (!result.TryAdd(key, extension))
            {
                // Replace
                result[key] = extension;
            }
        }

        return result;
    }

    /// <summary>
    /// Get all installed extensions for the provided package.
    /// </summary>
    Task<IEnumerable<InstalledPackageExtension>> GetInstalledExtensionsAsync(
        InstalledPackage installedPackage,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Like <see cref="GetInstalledExtensionsAsync"/>, but does not check version.
    /// </summary>
    Task<IEnumerable<InstalledPackageExtension>> GetInstalledExtensionsLiteAsync(
        InstalledPackage installedPackage,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Install an extension to the provided package.
    /// </summary>
    Task InstallExtensionAsync(
        PackageExtension extension,
        InstalledPackage installedPackage,
        PackageExtensionVersion? version = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an installed extension to the provided version.
    /// If no version is provided, the latest version will be used.
    /// </summary>
    Task UpdateExtensionAsync(
        InstalledPackageExtension installedExtension,
        InstalledPackage installedPackage,
        PackageExtensionVersion? version = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Uninstall an installed extension.
    /// </summary>
    Task UninstallExtensionAsync(
        InstalledPackageExtension installedExtension,
        InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
}
