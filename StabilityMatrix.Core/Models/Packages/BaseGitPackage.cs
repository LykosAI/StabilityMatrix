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

    public string DownloadLocation =>
        Path.Combine(SettingsManager.LibraryDir, "Packages", $"{Name}.zip");

    protected string GetDownloadUrl(DownloadPackageVersionOptions versionOptions)
    {
        if (!string.IsNullOrWhiteSpace(versionOptions.CommitHash))
        {
            return $"https://github.com/{Author}/{Name}/archive/{versionOptions.CommitHash}.zip";
        }

        if (!string.IsNullOrWhiteSpace(versionOptions.VersionTag))
        {
            return
                $"https://api.github.com/repos/{Author}/{Name}/zipball/{versionOptions.VersionTag}";
        }

        if (!string.IsNullOrWhiteSpace(versionOptions.BranchName))
        {
            return
                $"https://api.github.com/repos/{Author}/{Name}/zipball/{versionOptions.BranchName}";
        }
        
        throw new Exception("No download URL available");
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
    
    public override async Task<PackageVersionOptions> GetAllVersionOptions()
    {
        var packageVersionOptions = new PackageVersionOptions();
        
        var allReleases = await GetAllReleases().ConfigureAwait(false);
        var releasesList = allReleases.ToList();
        if (releasesList.Any())
        {
            packageVersionOptions.AvailableVersions = releasesList.Select(r =>
                new PackageVersion
                {
                    TagName = r.TagName!,
                    ReleaseNotesMarkdown = r.Body
                });
        }

        // Branch mode
        var allBranches = await GetAllBranches().ConfigureAwait(false);
        packageVersionOptions.AvailableBranches = allBranches.Select(b => new PackageVersion
        {
            TagName = $"{b.Name}",
            ReleaseNotesMarkdown = string.Empty
        });

        return packageVersionOptions;
    }

    /// <summary>
    /// Setup the virtual environment for the package.
    /// </summary>
    [MemberNotNull(nameof(VenvRunner))]
    public async Task<PyVenvRunner> SetupVenv(
        string installedPackagePath, 
        string venvName = "venv",
        bool forceRecreate = false)
    {
        var venvPath = Path.Combine(installedPackagePath, venvName);
        if (VenvRunner != null)
        {
            await VenvRunner.DisposeAsync().ConfigureAwait(false);
        }

        VenvRunner = new PyVenvRunner(venvPath)
        {
            WorkingDirectory = installedPackagePath,
            EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables,
        };
        
        if (!VenvRunner.Exists() || forceRecreate)
        {
            await VenvRunner.Setup(forceRecreate).ConfigureAwait(false);
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

    public override async Task DownloadPackage(string installLocation,
        DownloadPackageVersionOptions versionOptions, IProgress<ProgressReport>? progress = null)
    {
        var downloadUrl = GetDownloadUrl(versionOptions);

        if (!Directory.Exists(DownloadLocation.Replace($"{Name}.zip", "")))
        {
            Directory.CreateDirectory(DownloadLocation.Replace($"{Name}.zip", ""));
        }

        await DownloadService
            .DownloadToFileAsync(downloadUrl, DownloadLocation, progress: progress)
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(100, message: "Download Complete"));
    }

    public override async Task InstallPackage(string installLocation, IProgress<ProgressReport>? progress = null)
    {
        await UnzipPackage(installLocation, progress).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, $"{DisplayName} installed successfully"));
        File.Delete(DownloadLocation);
    }

    protected Task UnzipPackage(string installLocation, IProgress<ProgressReport>? progress = null)
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
        
                var folderPath = Path.Combine(installLocation,
                    entry.FullName.Replace(zipDirName, string.Empty));
                Directory.CreateDirectory(folderPath);
                continue;
            }
        
        
            var destinationPath = Path.GetFullPath(Path.Combine(installLocation,
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
        var currentVersion = package.Version;
        if (currentVersion == null)
        {
            return false;
        }

        try
        {
            if (currentVersion.IsReleaseMode)
            {
                var latestVersion = await GetLatestVersion().ConfigureAwait(false);
                UpdateAvailable = latestVersion != currentVersion.InstalledReleaseVersion;
                return UpdateAvailable;
            }

            var allCommits = (await GetAllCommits(currentVersion.InstalledBranch)
                .ConfigureAwait(false))?.ToList();
            if (allCommits == null || !allCommits.Any())
            {
                Logger.Warn("No commits found for {Package}", package.PackageName);
                return false;
            }
            var latestCommitHash = allCommits.First().Sha;
            return latestCommitHash != currentVersion.InstalledCommitSha;

        }
        catch (ApiException e)
        {
            Logger.Warn(e, "Failed to check for package updates");
            return false;
        }
    }

    public override async Task<InstalledPackageVersion> Update(InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress = null, bool includePrerelease = false)
    {
        if (installedPackage.Version == null) throw new NullReferenceException("Version is null");
        
        if (installedPackage.Version.IsReleaseMode)
        {
            var releases = await GetAllReleases().ConfigureAwait(false);
            var latestRelease = releases.First(x => includePrerelease || !x.Prerelease);

            await DownloadPackage(installedPackage.FullPath,
                    new DownloadPackageVersionOptions {VersionTag = latestRelease.TagName},
                    progress)
                .ConfigureAwait(false);
            
            await InstallPackage(installedPackage.FullPath, progress).ConfigureAwait(false);
            
            return new InstalledPackageVersion
            {
                InstalledReleaseVersion = latestRelease.TagName 
            };
        }

        // Commit mode
        var allCommits = await GetAllCommits(
            installedPackage.Version.InstalledBranch).ConfigureAwait(false);
        var latestCommit = allCommits?.First();

        if (latestCommit is null || string.IsNullOrEmpty(latestCommit.Sha))
        {
            throw new Exception("No commits found for branch");
        }

        await DownloadPackage(installedPackage.FullPath,
                new DownloadPackageVersionOptions {CommitHash = latestCommit.Sha}, progress)
            .ConfigureAwait(false);
        await InstallPackage(installedPackage.FullPath, progress).ConfigureAwait(false);
        
        return new InstalledPackageVersion
        {
            InstalledBranch = installedPackage.Version.InstalledBranch,
            InstalledCommitSha = latestCommit.Sha
        };
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
