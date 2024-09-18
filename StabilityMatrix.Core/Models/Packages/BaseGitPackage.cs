using System.Diagnostics;
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

    public virtual string RepositoryName => Name;
    public virtual string RepositoryAuthor => Author;

    /// <summary>
    /// URL of the hosted web page on launch
    /// </summary>
    protected string WebUrl = string.Empty;

    public override string GithubUrl => $"https://github.com/{RepositoryAuthor}/{RepositoryName}";

    public string DownloadLocation => Path.Combine(SettingsManager.LibraryDir, "Packages", $"{Name}.zip");

    protected string GetDownloadUrl(DownloadPackageVersionOptions versionOptions)
    {
        if (!string.IsNullOrWhiteSpace(versionOptions.CommitHash))
        {
            return $"https://github.com/{RepositoryAuthor}/{RepositoryName}/archive/{versionOptions.CommitHash}.zip";
        }

        if (!string.IsNullOrWhiteSpace(versionOptions.VersionTag))
        {
            return $"https://api.github.com/repos/{RepositoryAuthor}/{RepositoryName}/zipball/{versionOptions.VersionTag}";
        }

        if (!string.IsNullOrWhiteSpace(versionOptions.BranchName))
        {
            return $"https://api.github.com/repos/{RepositoryAuthor}/{RepositoryName}/zipball/{versionOptions.BranchName}";
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
            var commits = await GithubApi
                .GetAllCommits(RepositoryAuthor, RepositoryName, MainBranch)
                .ConfigureAwait(false);
            return new DownloadPackageVersionOptions
            {
                IsLatest = true,
                IsPrerelease = false,
                BranchName = MainBranch,
                CommitHash = commits?.FirstOrDefault()?.Sha ?? "unknown"
            };
        }

        var releases = await GithubApi.GetAllReleases(RepositoryAuthor, RepositoryName).ConfigureAwait(false);
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
        return GithubApi.GetAllCommits(RepositoryAuthor, RepositoryName, branch, page, perPage);
    }

    public override async Task<PackageVersionOptions> GetAllVersionOptions()
    {
        var packageVersionOptions = new PackageVersionOptions();

        if (!ShouldIgnoreReleases)
        {
            var allReleases = await GithubApi
                .GetAllReleases(RepositoryAuthor, RepositoryName)
                .ConfigureAwait(false);
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
        var allBranches = await GithubApi
            .GetAllBranches(RepositoryAuthor, RepositoryName)
            .ConfigureAwait(false);
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
        if (Interlocked.Exchange(ref VenvRunner, null) is { } oldRunner)
        {
            await oldRunner.DisposeAsync().ConfigureAwait(false);
        }

        var venvRunner = await SetupVenvPure(installedPackagePath, venvName, forceRecreate, onConsoleOutput)
            .ConfigureAwait(false);

        if (Interlocked.Exchange(ref VenvRunner, venvRunner) is { } oldRunner2)
        {
            await oldRunner2.DisposeAsync().ConfigureAwait(false);
        }

        Debug.Assert(VenvRunner != null, "VenvRunner != null");

        return venvRunner;
    }

    /// <summary>
    /// Like <see cref="SetupVenv"/>, but does not set the <see cref="VenvRunner"/> property.
    /// Returns a new <see cref="PyVenvRunner"/> instance.
    /// </summary>
    public async Task<PyVenvRunner> SetupVenvPure(
        string installedPackagePath,
        string venvName = "venv",
        bool forceRecreate = false,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        var venvRunner = await PyBaseInstall
            .Default.CreateVenvRunnerAsync(
                Path.Combine(installedPackagePath, venvName),
                workingDirectory: installedPackagePath,
                environmentVariables: SettingsManager.Settings.EnvironmentVariables,
                withDefaultTclTkEnv: Compat.IsWindows,
                withQueriedTclTkEnv: Compat.IsUnix
            )
            .ConfigureAwait(false);

        if (forceRecreate || !venvRunner.Exists())
        {
            await venvRunner.Setup(true, onConsoleOutput).ConfigureAwait(false);
        }

        return venvRunner;
    }

    public override async Task<IEnumerable<Release>> GetReleaseTags()
    {
        var allReleases = await GithubApi
            .GetAllReleases(RepositoryAuthor, RepositoryName)
            .ConfigureAwait(false);
        return allReleases;
    }

    public override async Task DownloadPackage(
        string installLocation,
        DownloadPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var versionOptions = options.VersionOptions;

        const long fiveGigs = 5 * SystemInfo.Gibibyte;

        if (SystemInfo.GetDiskFreeSpaceBytes(installLocation) is < fiveGigs)
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

                var folderPath = Path.Combine(
                    installLocation,
                    entry.FullName.Replace(zipDirName, string.Empty)
                );
                Directory.CreateDirectory(folderPath);
                continue;
            }

            var destinationPath = Path.GetFullPath(
                Path.Combine(installLocation, entry.FullName.Replace(zipDirName, string.Empty))
            );
            entry.ExtractToFile(destinationPath, true);

            progress?.Report(
                new ProgressReport(
                    current: Convert.ToUInt64(currentEntry),
                    total: Convert.ToUInt64(totalEntries)
                )
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

            var allCommits = (
                await GetAllCommits(currentVersion.InstalledBranch!).ConfigureAwait(false)
            )?.ToList();

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

    public override async Task<DownloadPackageVersionOptions?> GetUpdate(InstalledPackage installedPackage)
    {
        var currentVersion = installedPackage.Version;
        if (currentVersion is null or { InstalledReleaseVersion: null, InstalledBranch: null })
        {
            Logger.Warn(
                "Could not check updates for package {Name}, version is invalid: {@currentVersion}",
                Name,
                currentVersion
            );
            return null;
        }

        var versionOptions = new DownloadPackageVersionOptions { IsLatest = true };

        try
        {
            if (currentVersion.IsReleaseMode)
            {
                var latestVersion = await GetLatestVersion(currentVersion.IsPrerelease).ConfigureAwait(false);
                versionOptions.IsPrerelease = latestVersion.IsPrerelease;
                versionOptions.VersionTag = latestVersion.VersionTag;
                return versionOptions;
            }

            var allCommits = (
                await GetAllCommits(currentVersion.InstalledBranch!).ConfigureAwait(false)
            )?.ToList();

            if (allCommits == null || !allCommits.Any())
            {
                Logger.Warn("No commits found for {Package}", installedPackage.PackageName);
                return null;
            }
            var latestCommitHash = allCommits.First().Sha;

            versionOptions.CommitHash = latestCommitHash;
            versionOptions.BranchName = currentVersion.InstalledBranch;

            return versionOptions;
        }
        catch (ApiException e)
        {
            Logger.Warn(e, "Failed to check for package updates");
            return null;
        }
    }

    public override async Task<InstalledPackageVersion> Update(
        string installLocation,
        InstalledPackage installedPackage,
        UpdatePackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        if (installedPackage.Version == null)
            throw new NullReferenceException("Version is null");

        if (!Directory.Exists(Path.Combine(installedPackage.FullPath!, ".git")))
        {
            Logger.Info("not a git repo, initializing...");
            progress?.Report(new ProgressReport(-1f, "Initializing git repo", isIndeterminate: true));
            await PrerequisiteHelper
                .RunGit("init", onConsoleOutput, installedPackage.FullPath)
                .ConfigureAwait(false);
            await PrerequisiteHelper
                .RunGit(
                    new[] { "remote", "add", "origin", GithubUrl },
                    onConsoleOutput,
                    installedPackage.FullPath
                )
                .ConfigureAwait(false);
        }

        var versionOptions = options.VersionOptions;

        if (!string.IsNullOrWhiteSpace(versionOptions.VersionTag))
        {
            progress?.Report(new ProgressReport(-1f, "Fetching tags...", isIndeterminate: true));
            await PrerequisiteHelper
                .RunGit(new[] { "fetch", "--tags", "--force" }, onConsoleOutput, installedPackage.FullPath)
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
                    installLocation,
                    installedPackage,
                    options.AsInstallOptions(),
                    progress,
                    onConsoleOutput,
                    cancellationToken
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
        await PrerequisiteHelper
            .RunGit(new[] { "fetch", "--force" }, onConsoleOutput, installedPackage.FullPath)
            .ConfigureAwait(false);

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
                installLocation,
                installedPackage,
                options.AsInstallOptions(),
                progress,
                onConsoleOutput,
                cancellationToken
            )
            .ConfigureAwait(false);

        return new InstalledPackageVersion
        {
            InstalledBranch = versionOptions.BranchName,
            InstalledCommitSha = versionOptions.CommitHash,
            IsPrerelease = versionOptions.IsPrerelease
        };
    }

    private async Task FixInfinityFolders(DirectoryPath rootDirectory, string infinityFolderName)
    {
        // Skip if first infinity not found
        if (
            rootDirectory.JoinDir(infinityFolderName)
            is not { Exists: true, IsSymbolicLink: false } firstInfinity
        )
        {
            return;
        }

        var depth = 0;
        var currentDir = rootDirectory;

        while (currentDir.JoinDir(infinityFolderName) is { Exists: true, IsSymbolicLink: false } newInfinity)
        {
            depth++;
            currentDir = newInfinity;
        }

        Logger.Info("Found {Depth} infinity folders from {FirstPath}", depth, firstInfinity.ToString());

        // Move all items in infinity folder to root
        Logger.Info("Moving infinity folders content to root: {Path}", currentDir.ToString());
        await FileTransfers.MoveAllFilesAndDirectories(currentDir, rootDirectory).ConfigureAwait(false);

        // Move any files from first infinity by enumeration just in case
        foreach (var file in firstInfinity.EnumerateFiles())
        {
            await file.MoveToDirectoryAsync(rootDirectory).ConfigureAwait(false);
        }

        // Delete infinity folders chain from first
        Logger.Info("Deleting infinity folders: {Path}", currentDir.ToString());
        await firstInfinity.DeleteAsync(true).ConfigureAwait(false);
    }

    public override async Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        if (sharedFolderMethod != SharedFolderMethod.Symlink || SharedFolders is not { } sharedFolders)
        {
            return;
        }

        var modelsDir = new DirectoryPath(SettingsManager.ModelsDirectory);

        // fix infinity controlnet folders
        await FixInfinityFolders(modelsDir.JoinDir("ControlNet"), "ControlNet").ConfigureAwait(false);

        // fix duplicate links in models dir
        // see https://github.com/LykosAI/StabilityMatrix/issues/338
        string[] duplicatePaths =
        [
            Path.Combine("ControlNet", "ControlNet"),
            Path.Combine("IPAdapter", "base"),
            Path.Combine("IPAdapter", "sd15"),
            Path.Combine("IPAdapter", "sdxl")
        ];

        foreach (var duplicatePath in duplicatePaths)
        {
            var linkDir = modelsDir.JoinDir(duplicatePath);
            if (!linkDir.IsSymbolicLink)
                continue;

            Logger.Info("Removing duplicate junction at {Path}", linkDir.ToString());
            await linkDir.DeleteAsync(false).ConfigureAwait(false);
        }

        await Helper
            .SharedFolders.UpdateLinksForPackage(
                sharedFolders,
                SettingsManager.ModelsDirectory,
                installDirectory
            )
            .ConfigureAwait(false);
    }

    public override Task UpdateModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) => SetupModelFolders(installDirectory, sharedFolderMethod);

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        if (SharedFolders is not null && sharedFolderMethod == SharedFolderMethod.Symlink)
        {
            Helper.SharedFolders.RemoveLinksForPackage(SharedFolders, installDirectory);
        }
        return Task.CompletedTask;
    }

    public override Task SetupOutputFolderLinks(DirectoryPath installDirectory)
    {
        if (SharedOutputFolders is { } sharedOutputFolders)
        {
            return Helper.SharedFolders.UpdateLinksForPackage(
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
            Helper.SharedFolders.RemoveLinksForPackage(sharedOutputFolders, installDirectory);
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
