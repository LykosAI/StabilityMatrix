using System.Diagnostics.CodeAnalysis;
using Octokit;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public abstract class BasePackage(ISettingsManager settingsManager)
{
    protected readonly ISettingsManager SettingsManager = settingsManager;

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

    public abstract IEnumerable<TorchIndex> AvailableTorchIndices { get; }

    public virtual bool IsCompatible => GetRecommendedTorchVersion() != TorchIndex.Cpu;

    public abstract PackageDifficulty InstallerSortOrder { get; }

    public virtual PackageType PackageType => PackageType.SdInference;

    public abstract Task DownloadPackage(
        string installLocation,
        DownloadPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );

    public abstract Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    public abstract Task<bool> CheckForUpdates(InstalledPackage package);

    public abstract Task<InstalledPackageVersion> Update(
        string installLocation,
        InstalledPackage installedPackage,
        UpdatePackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    public abstract Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    );

    public virtual IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.Configuration, SharedFolderMethod.None };

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

    public virtual TorchIndex GetRecommendedTorchVersion()
    {
        // if there's only one AvailableTorchVersion, return that
        if (AvailableTorchIndices.Count() == 1)
        {
            return AvailableTorchIndices.First();
        }

        var preferNvidia = SettingsManager.Settings.PreferredGpu?.IsNvidia ?? HardwareHelper.HasNvidiaGpu();
        if (AvailableTorchIndices.Contains(TorchIndex.Cuda) && preferNvidia)
        {
            return TorchIndex.Cuda;
        }

        var preferAmd = SettingsManager.Settings.PreferredGpu?.IsAmd ?? HardwareHelper.HasAmdGpu();
        if (AvailableTorchIndices.Contains(TorchIndex.Zluda) && preferAmd)
        {
            return TorchIndex.Zluda;
        }

        var preferIntel = SettingsManager.Settings.PreferredGpu?.IsIntel ?? HardwareHelper.HasIntelGpu();
        if (AvailableTorchIndices.Contains(TorchIndex.Ipex) && preferIntel)
        {
            return TorchIndex.Ipex;
        }

        var preferRocm =
            Compat.IsLinux && (SettingsManager.Settings.PreferredGpu?.IsAmd ?? HardwareHelper.PreferRocm());
        if (AvailableTorchIndices.Contains(TorchIndex.Rocm) && preferRocm)
        {
            return TorchIndex.Rocm;
        }

        var preferDirectMl =
            Compat.IsWindows
            && (SettingsManager.Settings.PreferredGpu?.IsAmd ?? HardwareHelper.PreferDirectMLOrZluda());
        if (AvailableTorchIndices.Contains(TorchIndex.DirectMl) && preferDirectMl)
        {
            return TorchIndex.DirectMl;
        }

        if (Compat.IsMacOS && Compat.IsArm && AvailableTorchIndices.Contains(TorchIndex.Mps))
        {
            return TorchIndex.Mps;
        }

        return TorchIndex.Cpu;
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
    public virtual IReadOnlyList<string> ExtraLaunchArguments { get; } = Array.Empty<string>();

    /// <summary>
    /// The shared folders that this package supports.
    /// Mapping of <see cref="SharedFolderType"/> to the relative paths from the package root.
    /// </summary>
    public abstract Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public abstract Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders { get; }

    /// <summary>
    /// If defined, this package supports extensions using this manager.
    /// </summary>
    public virtual IPackageExtensionManager? ExtensionManager => null;

    /// <summary>
    /// True if this package supports extensions.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ExtensionManager))]
    public virtual bool SupportsExtensions => ExtensionManager is not null;

    public abstract Task<PackageVersionOptions> GetAllVersionOptions();
    public abstract Task<IEnumerable<GitCommit>?> GetAllCommits(
        string branch,
        int page = 1,
        int perPage = 10
    );
    public abstract Task<DownloadPackageVersionOptions?> GetLatestVersion(bool includePrerelease = false);
    public abstract string MainBranch { get; }
    public event EventHandler<int>? Exited;
    public event EventHandler<string>? StartupComplete;

    public void OnExit(int exitCode) => Exited?.Invoke(this, exitCode);

    public void OnStartupComplete(string url) => StartupComplete?.Invoke(this, url);

    public virtual PackageVersionType AvailableVersionTypes =>
        ShouldIgnoreReleases
            ? PackageVersionType.Commit
            : PackageVersionType.GithubRelease | PackageVersionType.Commit;

    public virtual IEnumerable<PackagePrerequisite> Prerequisites =>
        [
            PackagePrerequisite.Git,
            PackagePrerequisite.Python310,
            PackagePrerequisite.VcRedist,
            PackagePrerequisite.VcBuildTools
        ];

    protected async Task InstallCudaTorch(
        PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(new ProgressReport(-1f, "Installing PyTorch for CUDA", isIndeterminate: true));

        await venvRunner
            .PipInstall(
                new PipInstallArgs()
                    .WithTorch("==2.1.2")
                    .WithTorchVision("==0.16.2")
                    .WithXFormers("==0.0.23post1")
                    .WithTorchExtraIndex("cu121"),
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
        progress?.Report(new ProgressReport(-1f, "Installing PyTorch for DirectML", isIndeterminate: true));

        return venvRunner.PipInstall(new PipInstallArgs().WithTorchDirectML(), onConsoleOutput);
    }

    protected Task InstallCpuTorch(
        PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(new ProgressReport(-1f, "Installing PyTorch for CPU", isIndeterminate: true));

        return venvRunner.PipInstall(
            new PipInstallArgs().WithTorch("==2.1.2").WithTorchVision(),
            onConsoleOutput
        );
    }

    public abstract Task<DownloadPackageVersionOptions?> GetUpdate(InstalledPackage installedPackage);
}
