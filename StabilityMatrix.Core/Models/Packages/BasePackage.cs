using Octokit;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;

namespace StabilityMatrix.Core.Models.Packages;

public abstract class BasePackage
{
    public string ByAuthor => $"By {Author}";

    public abstract string Name { get; }
    public abstract string DisplayName { get; set; }
    public abstract string Author { get; }
    public abstract string Blurb { get; }
    public abstract string GithubUrl { get; }
    public abstract string LicenseType { get; }
    public abstract string LicenseUrl { get; }
    
    /// <summary>
    /// Primary command to launch the package. 'Launch' buttons uses this.
    /// </summary>
    public abstract string LaunchCommand { get; }
    
    /// <summary>
    /// Optional commands (e.g. 'config') that are on the launch button split drop-down.
    /// </summary>
    public virtual IReadOnlyList<string> ExtraLaunchCommands { get; } = Array.Empty<string>();
    
    public abstract Uri PreviewImageUri { get; }
    public virtual bool ShouldIgnoreReleases => false;
    public virtual bool UpdateAvailable { get; set; }

    public abstract Task<string> DownloadPackage(string version, bool isCommitHash,
        IProgress<ProgressReport>? progress = null);
    public abstract Task InstallPackage(IProgress<ProgressReport>? progress = null);
    public abstract Task RunPackage(string installedPackagePath, string command, string arguments);
    public abstract Task SetupModelFolders(DirectoryPath installDirectory);
    public abstract Task UpdateModelFolders(DirectoryPath installDirectory);
    
    /// <summary>
    /// Shuts down the subprocess, canceling any pending streams.
    /// </summary>
    public abstract void Shutdown();

    /// <summary>
    /// Shuts down the process, returning a Task to wait for output EOF.
    /// </summary>
    public abstract Task WaitForShutdown();
    
    public abstract Task<bool> CheckForUpdates(InstalledPackage package);

    public abstract Task<string> Update(InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress = null, bool includePrerelease = false);
    public abstract Task<IEnumerable<Release>> GetReleaseTags();

    public abstract List<LaunchOptionDefinition> LaunchOptions { get; }
    public virtual string? ExtraLaunchArguments { get; set; } = null;
    
    /// <summary>
    /// The shared folders that this package supports.
    /// Mapping of <see cref="SharedFolderType"/> to the relative paths from the package root.
    /// </summary>
    public virtual Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    
    public abstract Task<string> GetLatestVersion();
    public abstract Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true);
    public abstract Task<IEnumerable<GitCommit>?> GetAllCommits(string branch, int page = 1, int perPage = 10);
    public abstract Task<IEnumerable<Branch>> GetAllBranches();
    public abstract Task<IEnumerable<Release>> GetAllReleases();

    public abstract string DownloadLocation { get; }
    public abstract string InstallLocation { get; set; }

    public event EventHandler<ProcessOutput>? ConsoleOutput;
    public event EventHandler<int>? Exited;
    public event EventHandler<string>? StartupComplete;

    public void OnConsoleOutput(ProcessOutput output) => ConsoleOutput?.Invoke(this, output);
    public void OnExit(int exitCode) => Exited?.Invoke(this, exitCode);
    public void OnStartupComplete(string url) => StartupComplete?.Invoke(this, url);
}
