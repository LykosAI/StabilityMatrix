using Octokit;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;

namespace StabilityMatrix.Core.Models.Packages;

public class UnknownPackage : BasePackage
{
    public static string Key => "unknown-package";
    public override string Name => Key;
    public override string DisplayName { get; set; } = "Unknown Package";
    public override string Author => "";

    public override string GithubUrl => "";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => 
        "https://github.com/LykosAI/StabilityMatrix/blob/main/LICENSE";
    public override string Blurb => "A dank interface for diffusion";
    public override string LaunchCommand => "test";
    
    public override Uri PreviewImageUri => new("");
    
    public override IReadOnlyList<string> ExtraLaunchCommands => new[]
    {
        "test-config",
    };

    public override Task DownloadPackage(string installLocation, DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress1)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task InstallPackage(string installLocation, IProgress<ProgressReport>? progress = null)
    {
        throw new NotImplementedException();
    }

    public override Task RunPackage(string installedPackagePath, string command, string arguments)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task SetupModelFolders(DirectoryPath installDirectory)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task UpdateModelFolders(DirectoryPath installDirectory)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task RemoveModelFolderLinks(DirectoryPath installDirectory)
    {
        throw new NotImplementedException();
    }

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
    public override Task<InstalledPackageVersion> Update(InstalledPackage installedPackage, IProgress<ProgressReport>? progress = null,
        bool includePrerelease = false)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Release>> GetReleaseTags() => Task.FromResult(Enumerable.Empty<Release>());

    public override List<LaunchOptionDefinition> LaunchOptions => new();
    public override Task<string> GetLatestVersion() => Task.FromResult(string.Empty);

    public override Task<PackageVersionOptions> GetAllVersionOptions() =>
        Task.FromResult(new PackageVersionOptions());

    /// <inheritdoc />
    public override Task<IEnumerable<GitCommit>?> GetAllCommits(string branch, int page = 1, int perPage = 10) => Task.FromResult<IEnumerable<GitCommit>?>(null);

    /// <inheritdoc />
    public override Task<IEnumerable<Branch>> GetAllBranches() => Task.FromResult(Enumerable.Empty<Branch>());

    /// <inheritdoc />
    public override Task<IEnumerable<Release>> GetAllReleases() => Task.FromResult(Enumerable.Empty<Release>());
}
