using Octokit;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;

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
    public virtual string Disclaimer => string.Empty;
    public virtual bool OfferInOneClickInstaller => true;

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

    public virtual bool IsInferenceCompatible => false;

    public abstract string OutputFolderName { get; }

    public abstract IEnumerable<TorchVersion> AvailableTorchVersions { get; }

    public virtual bool IsCompatible => GetRecommendedTorchVersion() != TorchVersion.Cpu;

    public abstract PackageDifficulty InstallerSortOrder { get; }

    public abstract Task DownloadPackage(
        string installLocation,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress1
    );

    public abstract Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        SharedFolderMethod selectedSharedFolderMethod,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    );

    public abstract Task RunPackage(
        string installedPackagePath,
        string command,
        string arguments,
        Action<ProcessOutput>? onConsoleOutput
    );

    public abstract Task<bool> CheckForUpdates(InstalledPackage package);

    public abstract Task<InstalledPackageVersion> Update(
        InstalledPackage installedPackage,
        TorchVersion torchVersion,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        bool includePrerelease = false,
        Action<ProcessOutput>? onConsoleOutput = null
    );

    public virtual IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[]
        {
            SharedFolderMethod.Symlink,
            SharedFolderMethod.Configuration,
            SharedFolderMethod.None
        };

    public abstract SharedFolderMethod RecommendedSharedFolderMethod { get; }

    public abstract Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    );

    public abstract Task UpdateModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    );

    public abstract Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    );

    public abstract Task SetupOutputFolderLinks(DirectoryPath installDirectory);
    public abstract Task RemoveOutputFolderLinks(DirectoryPath installDirectory);

    public virtual TorchVersion GetRecommendedTorchVersion()
    {
        // if there's only one AvailableTorchVersion, return that
        if (AvailableTorchVersions.Count() == 1)
        {
            return AvailableTorchVersions.First();
        }

        if (HardwareHelper.HasNvidiaGpu() && AvailableTorchVersions.Contains(TorchVersion.Cuda))
        {
            return TorchVersion.Cuda;
        }

        if (HardwareHelper.PreferRocm() && AvailableTorchVersions.Contains(TorchVersion.Rocm))
        {
            return TorchVersion.Rocm;
        }

        if (
            HardwareHelper.PreferDirectML()
            && AvailableTorchVersions.Contains(TorchVersion.DirectMl)
        )
        {
            return TorchVersion.DirectMl;
        }

        if (Compat.IsMacOS && Compat.IsArm && AvailableTorchVersions.Contains(TorchVersion.Mps))
        {
            return TorchVersion.Mps;
        }

        return TorchVersion.Cpu;
    }

    /// <summary>
    /// Shuts down the subprocess, canceling any pending streams.
    /// </summary>
    public abstract void Shutdown();

    /// <summary>
    /// Shuts down the process, returning a Task to wait for output EOF.
    /// </summary>
    public abstract Task WaitForShutdown();
    public abstract Task<IEnumerable<Release>> GetReleaseTags();

    public abstract List<LaunchOptionDefinition> LaunchOptions { get; }
    public virtual string? ExtraLaunchArguments { get; set; } = null;

    /// <summary>
    /// The shared folders that this package supports.
    /// Mapping of <see cref="SharedFolderType"/> to the relative paths from the package root.
    /// </summary>
    public abstract Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public abstract Dictionary<
        SharedOutputType,
        IReadOnlyList<string>
    >? SharedOutputFolders { get; }

    public abstract Task<PackageVersionOptions> GetAllVersionOptions();
    public abstract Task<IEnumerable<GitCommit>?> GetAllCommits(
        string branch,
        int page = 1,
        int perPage = 10
    );
    public abstract Task<DownloadPackageVersionOptions> GetLatestVersion(
        bool includePrerelease = false
    );
    public abstract string MainBranch { get; }
    public event EventHandler<int>? Exited;
    public event EventHandler<string>? StartupComplete;

    public void OnExit(int exitCode) => Exited?.Invoke(this, exitCode);

    public void OnStartupComplete(string url) => StartupComplete?.Invoke(this, url);

    public virtual PackageVersionType AvailableVersionTypes =>
        ShouldIgnoreReleases
            ? PackageVersionType.Commit
            : PackageVersionType.GithubRelease | PackageVersionType.Commit;

    protected async Task InstallCudaTorch(
        PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(
            new ProgressReport(-1f, "Installing PyTorch for CUDA", isIndeterminate: true)
        );

        await venvRunner
            .PipInstall(
                new PipInstallArgs()
                    .WithTorch("==2.0.1")
                    .WithTorchVision("==0.15.2")
                    .WithXFormers("==0.0.20")
                    .WithTorchExtraIndex("cu118"),
                onConsoleOutput
            )
            .ConfigureAwait(false);
    }

    protected Task InstallDirectMlTorch(
        PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(
            new ProgressReport(-1f, "Installing PyTorch for DirectML", isIndeterminate: true)
        );

        return venvRunner.PipInstall(new PipInstallArgs().WithTorchDirectML(), onConsoleOutput);
    }

    protected Task InstallCpuTorch(
        PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(
            new ProgressReport(-1f, "Installing PyTorch for CPU", isIndeterminate: true)
        );

        return venvRunner.PipInstall(
            new PipInstallArgs().WithTorch("==2.0.1").WithTorchVision(),
            onConsoleOutput
        );
    }
}
