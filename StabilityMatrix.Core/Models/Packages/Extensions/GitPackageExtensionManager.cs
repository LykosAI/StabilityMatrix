using KGySoft.CoreLibraries;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.Packages.Extensions;

public abstract class GitPackageExtensionManager(IPrerequisiteHelper prerequisiteHelper)
    : IPackageExtensionManager
{
    public abstract string RelativeInstallDirectory { get; }

    protected virtual IEnumerable<string> IndexRelativeDirectories => [RelativeInstallDirectory];

    public abstract Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
        ExtensionManifest manifest,
        CancellationToken cancellationToken = default
    );

    /// <inheritdoc />
    Task<IEnumerable<PackageExtension>> IPackageExtensionManager.GetManifestExtensionsAsync(
        ExtensionManifest manifest,
        CancellationToken cancellationToken
    )
    {
        return GetManifestExtensionsAsync(manifest, cancellationToken);
    }

    protected abstract IEnumerable<ExtensionManifest> GetManifests(InstalledPackage installedPackage);

    /// <inheritdoc />
    IEnumerable<ExtensionManifest> IPackageExtensionManager.GetManifests(InstalledPackage installedPackage)
    {
        return GetManifests(installedPackage);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InstalledPackageExtension>> GetInstalledExtensionsAsync(
        InstalledPackage installedPackage,
        CancellationToken cancellationToken = default
    )
    {
        if (installedPackage.FullPath is not { } packagePath)
        {
            return Enumerable.Empty<InstalledPackageExtension>();
        }

        var extensions = new List<InstalledPackageExtension>();

        // Search for installed extensions in the package's index directories.
        foreach (
            var indexDirectory in IndexRelativeDirectories.Select(
                path => new DirectoryPath(packagePath, path)
            )
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check subdirectories of the index directory
            foreach (var subDirectory in indexDirectory.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip if not valid git repository
                if (await prerequisiteHelper.CheckIsGitRepository(subDirectory).ConfigureAwait(false) != true)
                    continue;

                // Get git version
                var version = await prerequisiteHelper
                    .GetGitRepositoryVersion(subDirectory)
                    .ConfigureAwait(false);

                extensions.Add(
                    new InstalledPackageExtension
                    {
                        Paths = [subDirectory],
                        Version = new PackageExtensionVersion
                        {
                            Tag = version.Tag,
                            Branch = version.Branch,
                            CommitSha = version.CommitSha
                        }
                    }
                );
            }
        }

        return extensions;
    }

    /// <inheritdoc />
    public async Task InstallExtensionAsync(
        PackageExtension extension,
        InstalledPackage installedPackage,
        PackageExtensionVersion? version = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (installedPackage.FullPath is not { } packagePath)
            throw new ArgumentException("Package must have a valid path.", nameof(installedPackage));

        // Ensure type
        if (extension.InstallType?.ToLowerInvariant() != "git-clone")
        {
            throw new ArgumentException(
                $"Extension must have install type 'git-clone' but has '{extension.InstallType}'.",
                nameof(extension)
            );
        }

        // Git clone all files
        var cloneRoot = new DirectoryPath(packagePath, RelativeInstallDirectory);

        foreach (var repositoryUri in extension.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new ProgressReport(0f, $"Cloning {repositoryUri}", isIndeterminate: true));

            await prerequisiteHelper
                .CloneGitRepository(cloneRoot, repositoryUri.ToString(), version)
                .ConfigureAwait(false);

            progress?.Report(new ProgressReport(1f, $"Cloned {repositoryUri}"));
        }
    }

    /// <inheritdoc />
    public async Task UninstallExtensionAsync(
        InstalledPackageExtension installedExtension,
        InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var path in installedExtension.Paths.Where(p => p.Exists))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await path.DeleteAsync().ConfigureAwait(false);
        }
    }
}
