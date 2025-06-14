using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Octokit;
using StabilityMatrix.Core.Extensions;
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
    public virtual IReadOnlyDictionary<string, string> ExtraLaunchCommands { get; } =
        new Dictionary<string, string>();

    public abstract Uri PreviewImageUri { get; }
    public virtual bool ShouldIgnoreReleases => false;
    public virtual bool UpdateAvailable { get; set; }

    public virtual bool IsInferenceCompatible => false;

    public abstract string OutputFolderName { get; }

    public abstract IEnumerable<TorchIndex> AvailableTorchIndices { get; }

    public virtual bool IsCompatible => GetRecommendedTorchVersion() != TorchIndex.Cpu;

    public abstract PackageDifficulty InstallerSortOrder { get; }

    public virtual PackageType PackageType => PackageType.SdInference;
    public virtual bool UsesVenv => true;
    public virtual bool InstallRequiresAdmin => false;
    public virtual string? AdminRequiredReason => null;
    public virtual PyVersion RecommendedPythonVersion => PyInstallationManager.Python_3_10_17;

    /// <summary>
    /// Returns a list of extra commands that can be executed for this package.
    /// The function takes an InstalledPackage parameter to operate on a specific installation.
    /// </summary>
    public virtual List<ExtraPackageCommand> GetExtraCommands() => [];

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
    /// Layout of the shared folders. For both Symlink and Config.
    /// </summary>
    public virtual SharedFolderLayout? SharedFolderLayout { get; } = new();

    /// <summary>
    /// The shared folders that this package supports.
    /// Mapping of <see cref="SharedFolderType"/> to the relative paths from the package root.
    /// (Legacy format for Symlink only, computed from SharedFolderLayout.)
    /// </summary>
    public virtual Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders =>
        GetLegacySharedFolders();

    private Dictionary<SharedFolderType, IReadOnlyList<string>>? GetLegacySharedFolders()
    {
        if (SharedFolderLayout is null)
            return null;

        // Keep track of unique paths since symbolic links can't do multiple targets
        // So we'll ignore duplicates once they appear here
        var addedPaths = new HashSet<string>();
        var result = new Dictionary<SharedFolderType, IReadOnlyList<string>>();

        foreach (var rule in SharedFolderLayout.Rules)
        {
            // Ignore empty
            if (rule.TargetRelativePaths is not { Length: > 0 })
            {
                continue;
            }

            // If there are multi SourceTypes <-> TargetRelativePaths:
            // We'll add a sub-path later
            var isMultiSource = rule.SourceTypes.Length > 1;

            foreach (var folderTypeKey in rule.SourceTypes)
            {
                var existingList =
                    (ImmutableList<string>)
                        result.GetValueOrDefault(folderTypeKey, ImmutableList<string>.Empty);

                var folderName = folderTypeKey.GetStringValue();

                foreach (var path in rule.TargetRelativePaths)
                {
                    var currentPath = path;

                    if (isMultiSource)
                    {
                        // Add a sub-path for each source type
                        currentPath = $"{path}/{folderName}";
                    }

                    // Skip if the path is already in the list
                    if (existingList.Contains(currentPath))
                        continue;

                    // Skip if the path is already added globally
                    if (!addedPaths.Add(currentPath))
                        continue;

                    result[folderTypeKey] = existingList.Add(currentPath);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Represents a mapping of shared output types to their corresponding folder paths.
    /// This property defines where various output files, such as images or grids,
    /// are stored for the package. The dictionary keys represent specific
    /// output types, and the values are lists of associated folder paths.
    /// </summary>
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
            PackagePrerequisite.VcBuildTools,
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

    /// <summary>
    /// List of known vulnerabilities for this package
    /// </summary>
    public virtual IReadOnlyList<PackageVulnerability> KnownVulnerabilities { get; protected set; } =
        Array.Empty<PackageVulnerability>();

    /// <summary>
    /// Whether this package has any known vulnerabilities
    /// </summary>
    public bool HasVulnerabilities => KnownVulnerabilities.Any();

    /// <summary>
    /// Whether this package has any critical vulnerabilities
    /// </summary>
    public bool HasCriticalVulnerabilities =>
        KnownVulnerabilities.Any(v => v.Severity == VulnerabilitySeverity.Critical);

    /// <summary>
    /// Check for any new vulnerabilities from external sources
    /// </summary>
    public virtual Task CheckForVulnerabilities(CancellationToken cancellationToken = default)
    {
        // Base implementation does nothing - derived classes should implement their own vulnerability checking
        return Task.CompletedTask;
    }
}
