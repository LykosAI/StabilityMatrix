using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using Octokit;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Python;
using StabilityMatrix.Services;

namespace StabilityMatrix.Models.Packages;

/// <summary>
/// Base class for packages that are hosted on Github.
/// Author and Name should be the Github username and repository name respectively.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public abstract class BaseGitPackage : BasePackage
{
    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    protected readonly IGithubApiCache GithubApi;
    protected readonly ISettingsManager SettingsManager;
    protected readonly IDownloadService DownloadService;
    protected readonly IPrerequisiteHelper PrerequisiteHelper;
    protected PyVenvRunner? VenvRunner;
    
    /// <summary>
    /// URL of the hosted web page on launch
    /// </summary>
    protected string WebUrl = string.Empty;

    public override string GithubUrl => $"https://github.com/{Author}/{Name}";

    public override string DownloadLocation =>
        $"{SettingsManager.LibraryDir}\\Packages\\{Name}.zip";

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

    protected Task<Release> GetLatestRelease()
    {
        return GithubApi.GetLatestRelease(Author, Name);
    }
    
    public override Task<IReadOnlyList<Branch>> GetAllBranches()
    {
        return GithubApi.GetAllBranches(Author, Name);
    }
    
    public override Task<IOrderedEnumerable<Release>> GetAllReleases()
    {
        return GithubApi.GetAllReleases(Author, Name);
    }
    
    public override Task<IReadOnlyList<GitHubCommit>?> GetAllCommits(string branch, int page = 1, int perPage = 10)
    {
        return GithubApi.GetAllCommits(Author, Name, branch, page, perPage);
    }

    /// <summary>
    /// Setup the virtual environment for the package.
    /// </summary>
    /// <param name="installedPackagePath"></param>
    /// <param name="venvName"></param>
    /// <returns></returns>
    protected async Task<PyVenvRunner> SetupVenv(string installedPackagePath, string venvName = "venv")
    {
        var venvPath = Path.Combine(installedPackagePath, "venv");
        VenvRunner?.Dispose();
        VenvRunner = new PyVenvRunner(venvPath);
        if (!VenvRunner.Exists())
        {
            await VenvRunner.Setup();
        }
        return VenvRunner;
    }
    
    public override async Task<IOrderedEnumerable<Release>> GetReleaseTags()
    {
        var allReleases = await GithubApi.GetAllReleases(Author, Name);
        return allReleases;
    }

    public override async Task<string?> DownloadPackage(string version, bool isCommitHash,
        IProgress<ProgressReport>? progress = null)
    {
        var downloadUrl = GetDownloadUrl(version, isCommitHash);

        if (!Directory.Exists(DownloadLocation.Replace($"{Name}.zip", "")))
        {
            Directory.CreateDirectory(DownloadLocation.Replace($"{Name}.zip", ""));
        }

        await DownloadService.DownloadToFileAsync(downloadUrl, DownloadLocation, progress: progress);
        progress?.Report(new ProgressReport(100, message: "Download Complete"));

        return version;
    }

    public override async Task InstallPackage(IProgress<ProgressReport>? progress = null)
    {
        await UnzipPackage(progress);
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

    public override async Task<bool> CheckForUpdates(string installedPackageName)
    {
        var package =
            SettingsManager.Settings.InstalledPackages.FirstOrDefault(x => x.DisplayName == installedPackageName);
        var currentVersion = package?.PackageVersion;
        if (package == null || string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(package.InstalledBranch))
            {
                var latestVersion = await GetLatestVersion();
                UpdateAvailable = latestVersion != currentVersion;
                return latestVersion != currentVersion;
            }
            else
            {
                var allCommits = await GetAllCommits(package.InstalledBranch);
                if (allCommits == null || !allCommits.Any())
                {
                    Logger.Warn("No commits found for {Package}", package.PackageName);
                    return false;
                }
                var latestCommitHash = allCommits[0].Sha;
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
        IProgress<ProgressReport>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(installedPackage.InstalledBranch))
        {
            var releases = await GetAllReleases();
            var latestRelease = releases.First();
            await DownloadPackage(latestRelease.TagName, false, progress);
            await InstallPackage(progress);
            return latestRelease.TagName;
        }
        else
        {
            var allCommits = await GetAllCommits(installedPackage.InstalledBranch);
            var latestCommit = allCommits.First();
            await DownloadPackage(latestCommit.Sha, true, progress);
            await InstallPackage(progress);
            return latestCommit.Sha;
        }
    }


    public override Task Shutdown()
    {
        VenvRunner?.Dispose();
        VenvRunner?.Process?.WaitForExitAsync();
        return Task.CompletedTask;
    }
}
