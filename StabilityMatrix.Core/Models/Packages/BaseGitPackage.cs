using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using NLog;
using Octokit;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
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

    public string DownloadLocation => Path.Combine(SettingsManager.LibraryDir, "Packages", $"{Name}.zip");

    protected string GetDownloadUrl(DownloadPackageVersionOptions versionOptions)
    {
        if (!string.IsNullOrWhiteSpace(versionOptions.CommitHash))
        {
            return $"https://github.com/{Author}/{Name}/archive/{versionOptions.CommitHash}.zip";
        }

        if (!string.IsNullOrWhiteSpace(versionOptions.VersionTag))
        {
            return $"https://api.github.com/repos/{Author}/{Name}/zipball/{versionOptions.VersionTag}";
        }

        if (!string.IsNullOrWhiteSpace(versionOptions.BranchName))
        {
            return $"https://api.github.com/repos/{Author}/{Name}/zipball/{versionOptions.BranchName}";
        }

        throw new Exception("No download URL available");
    }

    protected BaseGitPackage(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper
    )
    {
        GithubApi = githubApi;
        SettingsManager = settingsManager;
        DownloadService = downloadService;
        PrerequisiteHelper = prerequisiteHelper;
    }

    public override async Task<DownloadPackageVersionOptions> GetLatestVersion(bool includePrerelease = false)
    {
        if (ShouldIgnoreReleases)
        {
            return new DownloadPackageVersionOptions
            {
                IsLatest = true,
                IsPrerelease = false,
                BranchName = MainBranch
            };
        }

        var releases = await GithubApi.GetAllReleases(Author, Name).ConfigureAwait(false);
        var latestRelease = includePrerelease ? releases.First() : releases.First(x => !x.Prerelease);

        return new DownloadPackageVersionOptions
        {
            IsLatest = true,
            IsPrerelease = latestRelease.Prerelease,
            VersionTag = latestRelease.TagName!
        };
    }

    public override Task<IEnumerable<GitCommit>?> GetAllCommits(string branch, int page = 1, int perPage = 10)
    {
        return GithubApi.GetAllCommits(Author, Name, branch, page, perPage);
    }

    public override async Task<PackageVersionOptions> GetAllVersionOptions()
    {
        var packageVersionOptions = new PackageVersionOptions();

        if (!ShouldIgnoreReleases)
        {
            var allReleases = await GithubApi.GetAllReleases(Author, Name).ConfigureAwait(false);
            var releasesList = allReleases.ToList();
            if (releasesList.Any())
            {
                packageVersionOptions.AvailableVersions = releasesList.Select(
                    r =>
                        new PackageVersion
                        {
                            TagName = r.TagName!,
                            ReleaseNotesMarkdown = r.Body,
                            IsPrerelease = r.Prerelease
                        }
                );
            }
        }

        // Branch mode
        var allBranches = await GithubApi.GetAllBranches(Author, Name).ConfigureAwait(false);
        packageVersionOptions.AvailableBranches = allBranches.Select(
            b => new PackageVersion { TagName = $"{b.Name}", ReleaseNotesMarkdown = string.Empty }
        );

        return packageVersionOptions;
    }

    /// <summary>
    /// Setup the virtual environment for the package.
    /// </summary>
    [MemberNotNull(nameof(VenvRunner))]
    public async Task<PyVenvRunner> SetupVenv(
        string installedPackagePath,
        string venvName = "venv",
        bool forceRecreate = false,
        Action<ProcessOutput>? onConsoleOutput = null
    )
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
            await VenvRunner.Setup(forceRecreate, onConsoleOutput).ConfigureAwait(false);
        }
        return VenvRunner;
    }

    public override async Task<IEnumerable<Release>> GetReleaseTags()
    {
        var allReleases = await GithubApi.GetAllReleases(Author, Name).ConfigureAwait(false);
        return allReleases;
    }

    public override async Task DownloadPackage(
        string installLocation,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null
    )
    {
        const long fiveGigs = 5 * SystemInfo.Gigabyte;
        if (SystemInfo.GetDiskFreeSpaceBytes(installLocation) < fiveGigs)
        {
            throw new ApplicationException(
                $"Not enough space to download {Name} to {installLocation}, need at least 5GB"
            );
        }

        await PrerequisiteHelper
            .RunGit(
                new[]
                {
                    "clone",
                    "--branch",
                    !string.IsNullOrWhiteSpace(versionOptions.VersionTag)
                        ? versionOptions.VersionTag
                        : versionOptions.BranchName ?? MainBranch,
                    GithubUrl,
                    installLocation
                }
            )
            .ConfigureAwait(false);

        if (!versionOptions.IsLatest && !string.IsNullOrWhiteSpace(versionOptions.CommitHash))
        {
            await PrerequisiteHelper
                .RunGit(new[] { "checkout", versionOptions.CommitHash }, installLocation)
                .ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(100, message: "Download Complete"));
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

                var folderPath = Path.Combine(installLocation, entry.FullName.Replace(zipDirName, string.Empty));
                Directory.CreateDirectory(folderPath);
                continue;
            }

            var destinationPath = Path.GetFullPath(
                Path.Combine(installLocation, entry.FullName.Replace(zipDirName, string.Empty))
            );
            entry.ExtractToFile(destinationPath, true);

            progress?.Report(
                new ProgressReport(current: Convert.ToUInt64(currentEntry), total: Convert.ToUInt64(totalEntries))
            );
        }

        return Task.CompletedTask;
    }

    public override async Task<bool> CheckForUpdates(InstalledPackage package)
    {
        var currentVersion = package.Version;
        if (currentVersion is null or { InstalledReleaseVersion: null, InstalledBranch: null })
        {
            Logger.Warn(
                "Could not check updates for package {Name}, version is invalid: {@currentVersion}",
                Name,
                currentVersion
            );
            return false;
        }

        try
        {
            if (currentVersion.IsReleaseMode)
            {
                var latestVersion = await GetLatestVersion(currentVersion.IsPrerelease).ConfigureAwait(false);
                UpdateAvailable = latestVersion.VersionTag != currentVersion.InstalledReleaseVersion;
                return UpdateAvailable;
            }

            var allCommits = (await GetAllCommits(currentVersion.InstalledBranch!).ConfigureAwait(false))?.ToList();

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

    public override async Task<InstalledPackageVersion> Update(
        InstalledPackage installedPackage,
        TorchVersion torchVersion,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        bool includePrerelease = false,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        if (installedPackage.Version == null)
            throw new NullReferenceException("Version is null");

        if (!Directory.Exists(Path.Combine(installedPackage.FullPath!, ".git")))
        {
            Logger.Info("not a git repo, initializing...");
            progress?.Report(new ProgressReport(-1f, "Initializing git repo", isIndeterminate: true));
            await PrerequisiteHelper.RunGit("init", onConsoleOutput, installedPackage.FullPath).ConfigureAwait(false);
            await PrerequisiteHelper
                .RunGit(new[] { "remote", "add", "origin", GithubUrl }, onConsoleOutput, installedPackage.FullPath)
                .ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(versionOptions.VersionTag))
        {
            progress?.Report(new ProgressReport(-1f, "Fetching tags...", isIndeterminate: true));
            await PrerequisiteHelper
                .RunGit(new[] { "fetch", "--tags" }, onConsoleOutput, installedPackage.FullPath)
                .ConfigureAwait(false);

            progress?.Report(
                new ProgressReport(-1f, $"Checking out {versionOptions.VersionTag}", isIndeterminate: true)
            );
            await PrerequisiteHelper
                .RunGit(
                    new[] { "checkout", versionOptions.VersionTag, "--force" },
                    onConsoleOutput,
                    installedPackage.FullPath
                )
                .ConfigureAwait(false);

            await InstallPackage(
                    installedPackage.FullPath!,
                    torchVersion,
                    installedPackage.PreferredSharedFolderMethod ?? SharedFolderMethod.Symlink,
                    versionOptions,
                    progress,
                    onConsoleOutput
                )
                .ConfigureAwait(false);

            return new InstalledPackageVersion
            {
                InstalledReleaseVersion = versionOptions.VersionTag,
                IsPrerelease = versionOptions.IsPrerelease
            };
        }

        // fetch
        progress?.Report(new ProgressReport(-1f, "Fetching data...", isIndeterminate: true));
        await PrerequisiteHelper.RunGit("fetch", onConsoleOutput, installedPackage.FullPath).ConfigureAwait(false);

        if (versionOptions.IsLatest)
        {
            // checkout
            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Checking out {installedPackage.Version.InstalledBranch}...",
                    isIndeterminate: true
                )
            );
            await PrerequisiteHelper
                .RunGit(
                    new[] { "checkout", versionOptions.BranchName!, "--force" },
                    onConsoleOutput,
                    installedPackage.FullPath
                )
                .ConfigureAwait(false);

            // pull
            progress?.Report(new ProgressReport(-1f, "Pulling changes...", isIndeterminate: true));
            await PrerequisiteHelper
                .RunGit(
                    new[] { "pull", "--autostash", "origin", installedPackage.Version.InstalledBranch! },
                    onConsoleOutput,
                    installedPackage.FullPath!
                )
                .ConfigureAwait(false);
        }
        else
        {
            // checkout
            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Checking out {installedPackage.Version.InstalledBranch}...",
                    isIndeterminate: true
                )
            );
            await PrerequisiteHelper
                .RunGit(
                    new[] { "checkout", versionOptions.CommitHash!, "--force" },
                    onConsoleOutput,
                    installedPackage.FullPath
                )
                .ConfigureAwait(false);
        }

        await InstallPackage(
                installedPackage.FullPath,
                torchVersion,
                installedPackage.PreferredSharedFolderMethod ?? SharedFolderMethod.Symlink,
                versionOptions,
                progress,
                onConsoleOutput
            )
            .ConfigureAwait(false);

        return new InstalledPackageVersion
        {
            InstalledBranch = versionOptions.BranchName,
            InstalledCommitSha = versionOptions.CommitHash,
            IsPrerelease = versionOptions.IsPrerelease
        };
    }

    public override Task SetupModelFolders(DirectoryPath installDirectory, SharedFolderMethod sharedFolderMethod)
    {
        if (sharedFolderMethod == SharedFolderMethod.Symlink && SharedFolders is { } folders)
        {
            return StabilityMatrix
                .Core
                .Helper
                .SharedFolders
                .UpdateLinksForPackage(folders, SettingsManager.ModelsDirectory, installDirectory);
        }

        return Task.CompletedTask;
    }

    public override Task UpdateModelFolders(DirectoryPath installDirectory, SharedFolderMethod sharedFolderMethod)
    {
        if (sharedFolderMethod == SharedFolderMethod.Symlink && SharedFolders is { } sharedFolders)
        {
            return StabilityMatrix
                .Core
                .Helper
                .SharedFolders
                .UpdateLinksForPackage(sharedFolders, SettingsManager.ModelsDirectory, installDirectory);
        }

        return Task.CompletedTask;
    }

    public override Task RemoveModelFolderLinks(DirectoryPath installDirectory, SharedFolderMethod sharedFolderMethod)
    {
        if (SharedFolders is not null && sharedFolderMethod == SharedFolderMethod.Symlink)
        {
            StabilityMatrix.Core.Helper.SharedFolders.RemoveLinksForPackage(SharedFolders, installDirectory);
        }
        return Task.CompletedTask;
    }

    public override Task SetupOutputFolderLinks(DirectoryPath installDirectory)
    {
        if (SharedOutputFolders is { } sharedOutputFolders)
        {
            return StabilityMatrix
                .Core
                .Helper
                .SharedFolders
                .UpdateLinksForPackage(
                    sharedOutputFolders,
                    SettingsManager.ImagesDirectory,
                    installDirectory,
                    recursiveDelete: true
                );
        }

        return Task.CompletedTask;
    }

    public override Task RemoveOutputFolderLinks(DirectoryPath installDirectory)
    {
        if (SharedOutputFolders is { } sharedOutputFolders)
        {
            StabilityMatrix.Core.Helper.SharedFolders.RemoveLinksForPackage(sharedOutputFolders, installDirectory);
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
