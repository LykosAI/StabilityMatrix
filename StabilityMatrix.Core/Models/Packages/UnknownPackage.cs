using Octokit;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.Packages;

public class UnknownPackage : BasePackage
{
    public static string Key => "unknown-package";
    public override string Name => Key;
    public override string DisplayName { get; set; } = "Unknown Package";
    public override string Author => "";

    public override string GithubUrl => "";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => "https://github.com/LykosAI/StabilityMatrix/blob/main/LICENSE";
    public override string Blurb => "A dank interface for diffusion";
    public override string LaunchCommand => "test";

    public override Uri PreviewImageUri => new("");

    public override IReadOnlyList<string> ExtraLaunchCommands => new[] { "test-config", };

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override string OutputFolderName { get; }

    public override PackageDifficulty InstallerSortOrder { get; }

    public override Task DownloadPackage(
        string installLocation,
        DownloadPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public override Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task UpdateModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        throw new NotImplementedException();
    }

    public override Task SetupOutputFolderLinks(DirectoryPath installDirectory)
    {
        throw new NotImplementedException();
    }

    public override Task RemoveOutputFolderLinks(DirectoryPath installDirectory)
    {
        throw new NotImplementedException();
    }

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        new[] { TorchIndex.Cuda, TorchIndex.Cpu, TorchIndex.Rocm, TorchIndex.DirectMl };

    /// <inheritdoc />
    public override void Shutdown()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task WaitForShutdown()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<bool> CheckForUpdates(InstalledPackage package)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<InstalledPackageVersion> Update(
        string installLocation,
        InstalledPackage installedPackage,
        UpdatePackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Release>> GetReleaseTags() =>
        Task.FromResult(Enumerable.Empty<Release>());

    public override List<LaunchOptionDefinition> LaunchOptions => new();

    public override Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders { get; }

    public override Task<DownloadPackageVersionOptions> GetLatestVersion(bool includePrerelease = false)
    {
        throw new NotImplementedException();
    }

    public override string MainBranch { get; }

    public override Task<DownloadPackageVersionOptions?> GetUpdate(InstalledPackage installedPackage)
    {
        return Task.FromResult(new DownloadPackageVersionOptions { IsLatest = true, VersionTag = "1.8.0" });
    }

    public override Task<PackageVersionOptions> GetAllVersionOptions() =>
        Task.FromResult(new PackageVersionOptions());

    /// <inheritdoc />
    public override Task<IEnumerable<GitCommit>?> GetAllCommits(
        string branch,
        int page = 1,
        int perPage = 10
    ) => Task.FromResult<IEnumerable<GitCommit>?>(null);
}
