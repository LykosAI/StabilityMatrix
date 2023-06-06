using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;

namespace StabilityMatrix.Models.Packages;

public abstract class BasePackage
{
    public string ByAuthor => $"By {Author}";

    public abstract string Name { get; }
    public abstract string DisplayName { get; set; }
    public abstract string Author { get; }
    public abstract string GithubUrl { get; }
    public abstract string LaunchCommand { get; }
    public abstract Uri PreviewImageUri { get; }
    public virtual bool ShouldIgnoreReleases => false;
    public virtual bool UpdateAvailable { get; set; }

    public abstract Task<string?> DownloadPackage(string version, bool isCommitHash,
        IProgress<ProgressReport>? progress = null);
    public abstract Task InstallPackage(IProgress<ProgressReport>? progress = null);
    public abstract Task RunPackage(string installedPackagePath, string arguments);
    public abstract Task Shutdown();
    public abstract Task<bool> CheckForUpdates(string installedPackageName);
    public abstract Task<string> Update(InstalledPackage installedPackage, IProgress<ProgressReport>? progress = null);
    public abstract Task<IOrderedEnumerable<Release>> GetReleaseTags();

    public abstract List<LaunchOptionDefinition> LaunchOptions { get; }
    public virtual string? ExtraLaunchArguments { get; set; } = null;
    
    /// <summary>
    /// The shared folders that this package supports.
    /// Mapping of <see cref="SharedFolderType"/> to the relative path from the package root.
    /// </summary>
    public virtual Dictionary<SharedFolderType, string>? SharedFolders { get; }
    
    public abstract Task<string> GetLatestVersion();
    public abstract Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true);
    public abstract Task<IReadOnlyList<GitHubCommit>?> GetAllCommits(string branch, int page = 1, int perPage = 10);
    public abstract Task<IReadOnlyList<Branch>> GetAllBranches();
    public abstract Task<IOrderedEnumerable<Release>> GetAllReleases();

    public abstract string DownloadLocation { get; }
    public abstract string InstallLocation { get; set; }

    public event EventHandler<string>? ConsoleOutput;
    public event EventHandler<int>? Exited;
    public event EventHandler<string>? StartupComplete;

    public void OnConsoleOutput(string output) => ConsoleOutput?.Invoke(this, output);
    public void OnExit(int exitCode) => Exited?.Invoke(this, exitCode);
    public void OnStartupComplete(string url) => StartupComplete?.Invoke(this, url);
}
