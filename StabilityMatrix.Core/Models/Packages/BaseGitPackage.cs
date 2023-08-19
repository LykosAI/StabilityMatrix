using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using NLog;
using Octokit;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

/// <summary>
/// Base class for packages that are hosted on Github.
/// Author and Name should be the Github username and repository name respectively.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
public abstract class BaseGitPackage : BasePackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    protected readonly IGithubApiCache GithubApi;
    protected readonly ISettingsManager SettingsManager;
    protected readonly IDownloadService DownloadService;
    protected readonly IPrerequisiteHelper PrerequisiteHelper;
    public PyVenvRunner? VenvRunner;
    
    /// <summary>
    /// URL of the hosted web page on launch
    /// </summary>
    protected string WebUrl = string.Empty;

    public override string GithubUrl => $"https://github.com/{Author}/{Name}";

    public override string DownloadLocation =>
        Path.Combine(SettingsManager.LibraryDir, "Packages", $"{Name}.zip");

    public override string InstallLocation { get; set; }

    protected string GetDownloadUrl(string tagName, bool isCommitHash = false)
    {
        return isCommitHash
            ? $"https://github.com/{Author}/{Name}/archive/{tagName}.zip"
            : $"https://api.github.com/repos/{Author}/{Name}/zipball/{tagName}";
    }

    protected BaseGitPackage(IGithubApiCache githubApi, ISettingsManager settingsManager,
        IDownloadService downloadService, IPrerequisiteHelper prerequisiteHelper)
    {
        GithubApi = githubApi;
        SettingsManager = settingsManager;
        DownloadService = downloadService;
        PrerequisiteHelper = prerequisiteHelper;
    }

    protected async Task<Release> GetLatestRelease(bool includePrerelease = false)
    {
        var releases = await GithubApi
            .GetAllReleases(Author, Name)
            .ConfigureAwait(false);
        return includePrerelease ? releases.First() : releases.First(x => !x.Prerelease);
    }
    
    public override Task<IEnumerable<Branch>> GetAllBranches()
    {
        return GithubApi.GetAllBranches(Author, Name);
    }
    
    public override Task<IEnumerable<Release>> GetAllReleases()
    {
        return GithubApi.GetAllReleases(Author, Name);
    }
    
    public override Task<IEnumerable<GitCommit>?> GetAllCommits(string branch, int page = 1, int perPage = 10)
    {
        return GithubApi.GetAllCommits(Author, Name, branch, page, perPage);
    }
    
    public override async Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true)
    {
        // Release mode
        if (isReleaseMode)
        {
            var allReleases = await GetAllReleases().ConfigureAwait(false);
            return allReleases.Where(r => r.Prerelease == false).Select(r => 
                new PackageVersion
                {
                    TagName = r.TagName!, 
                    ReleaseNotesMarkdown = r.Body
                });
        }

        // Branch mode
        var allBranches = await GetAllBranches().ConfigureAwait(false);
        return allBranches.Select(b => new PackageVersion
        {
            TagName = $"{b.Name}",
            ReleaseNotesMarkdown = string.Empty
        });
    }

    /// <summary>
    /// Setup the virtual environment for the package.
    /// </summary>
    /// <param name="installedPackagePath"></param>
    /// <param name="venvName"></param>
    /// <returns></returns>
    [MemberNotNull(nameof(VenvRunner))]
    protected async Task<PyVenvRunner> SetupVenv(string installedPackagePath, string venvName = "venv")
    {
        var venvPath = Path.Combine(installedPackagePath, "venv");
        if (VenvRunner != null)
        {
            await VenvRunner.DisposeAsync().ConfigureAwait(false);
        }

        VenvRunner = new PyVenvRunner(venvPath)
        {
            WorkingDirectory = installedPackagePath,
            EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables,
        };
        
        if (!VenvRunner.Exists())
        {
            await VenvRunner.Setup().ConfigureAwait(false);
        }
        return VenvRunner;
    }
    
    public override async Task<IEnumerable<Release>> GetReleaseTags()
    {
        var allReleases = await GithubApi
            .GetAllReleases(Author, Name)
            .ConfigureAwait(false);
        return allReleases;
    }

    public override async Task<string> DownloadPackage(string version, bool isCommitHash,
        IProgress<ProgressReport>? progress = null)
    {
        var downloadUrl = GetDownloadUrl(version, isCommitHash);

        if (!Directory.Exists(DownloadLocation.Replace($"{Name}.zip", "")))
        {
            Directory.CreateDirectory(DownloadLocation.Replace($"{Name}.zip", ""));
        }

        await DownloadService
            .DownloadToFileAsync(downloadUrl, DownloadLocation, progress: progress)
            .ConfigureAwait(false);
        
        progress?.Report(new ProgressReport(100, message: "Download Complete"));

        return version;
    }

    public override async Task InstallPackage(IProgress<ProgressReport>? progress = null)
    {
        await UnzipPackage(progress).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, $"{DisplayName} installed successfully"));
        File.Delete(DownloadLocation);
    }

    protected Task UnzipPackage(IProgress<ProgressReport>? progress = null)
    {
        using var zip = ZipFile.OpenRead(DownloadLocation);
        var zipDirName = string.Empty;
        var totalEntries = zip.Entries.Count;
        var currentEntry = 0;
        
        foreach (var entry in zip.Entries)
        {
            currentEntry++;
            if (string.IsNullOrWhiteSpace(entry.Name) && entry.FullName.EndsWith("/"))
            {
                if (string.IsNullOrWhiteSpace(zipDirName))
                {
                    zipDirName = entry.FullName;
                }
        
                var folderPath = Path.Combine(InstallLocation,
                    entry.FullName.Replace(zipDirName, string.Empty));
                Directory.CreateDirectory(folderPath);
                continue;
            }
        
        
            var destinationPath = Path.GetFullPath(Path.Combine(InstallLocation,
                entry.FullName.Replace(zipDirName, string.Empty)));
            entry.ExtractToFile(destinationPath, true);
        
            progress?.Report(new ProgressReport(current: Convert.ToUInt64(currentEntry),
                total: Convert.ToUInt64(totalEntries)));
        }
        
        progress?.Report(new ProgressReport(1f, $"{DisplayName} installed successfully"));

        return Task.CompletedTask;
    }

    public override async Task<bool> CheckForUpdates(InstalledPackage package)
    {
        var currentVersion = package.PackageVersion;
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(package.InstalledBranch))
            {
                var latestVersion = await GetLatestVersion().ConfigureAwait(false);
                UpdateAvailable = latestVersion != currentVersion;
                return latestVersion != currentVersion;
            }
            else
            {
                var allCommits = (await GetAllCommits(package.InstalledBranch)
                    .ConfigureAwait(false))?.ToList();
                if (allCommits == null || !allCommits.Any())
                {
                    Logger.Warn("No commits found for {Package}", package.PackageName);
                    return false;
                }
                var latestCommitHash = allCommits.First().Sha;
                return latestCommitHash != currentVersion;
            }
            
        }
        catch (ApiException e)
        {
            Logger.Warn(e, "Failed to check for package updates");
            return false;
        }
    }

    public override async Task<string> Update(InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress = null, bool includePrerelease = false)
    {
        // Release mode
        if (string.IsNullOrWhiteSpace(installedPackage.InstalledBranch))
        {
            var releases = await GetAllReleases().ConfigureAwait(false);
            var latestRelease = releases.First(x => includePrerelease || !x.Prerelease);
            await DownloadPackage(latestRelease.TagName, false, progress).ConfigureAwait(false);
            await InstallPackage(progress).ConfigureAwait(false);
            return latestRelease.TagName;
        }

        // Commit mode
        var allCommits = await GetAllCommits(
            installedPackage.InstalledBranch).ConfigureAwait(false);
        var latestCommit = allCommits?.First();

        if (latestCommit is null || string.IsNullOrEmpty(latestCommit.Sha))
        {
            throw new Exception("No commits found for branch");
        }
            
        await DownloadPackage(latestCommit.Sha, true, progress).ConfigureAwait(false);
        await InstallPackage(progress).ConfigureAwait(false);
        return latestCommit.Sha;
    }
    
    public override Task SetupModelFolders(DirectoryPath installDirectory)
    {
        if (SharedFolders is { } folders)
        {
            StabilityMatrix.Core.Helper.SharedFolders
                .SetupLinks(folders, SettingsManager.ModelsDirectory, installDirectory);
        }
        return Task.CompletedTask;
    }

    public override async Task UpdateModelFolders(DirectoryPath installDirectory)
    {
        if (SharedFolders is not null)
        {
            await StabilityMatrix.Core.Helper.SharedFolders.UpdateLinksForPackage(this,
                SettingsManager.ModelsDirectory, installDirectory).ConfigureAwait(false);
        }
    }

    public override Task RemoveModelFolderLinks(DirectoryPath installDirectory)
    {
        if (SharedFolders is not null)
        {
            StabilityMatrix.Core.Helper.SharedFolders.RemoveLinksForPackage(this, installDirectory);
        }
        return Task.CompletedTask;
    }

    // Send input to the running process.
    public virtual void SendInput(string input)
    {
        var process = VenvRunner?.Process;
        if (process == null)
        {
            Logger.Warn("No process running for {Name}", Name);
            return;
        }
        process.StandardInput.WriteLine(input);
    }
    
    public virtual async Task SendInputAsync(string input)
    {
        var process = VenvRunner?.Process;
        if (process == null)
        {
            Logger.Warn("No process running for {Name}", Name);
            return;
        }
        await process.StandardInput.WriteLineAsync(input).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Shutdown()
    {
        if (VenvRunner is not null)
        {
            VenvRunner.Dispose();
            VenvRunner = null;
        }
    }
    
    /// <inheritdoc />
    public override async Task WaitForShutdown()
    {
        if (VenvRunner is not null)
        {
            await VenvRunner.DisposeAsync().ConfigureAwait(false);
            VenvRunner = null;
        }
    }
}
