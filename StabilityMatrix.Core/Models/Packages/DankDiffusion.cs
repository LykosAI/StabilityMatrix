using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class DankDiffusion : BaseGitPackage
{
    public DankDiffusion(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper,
        IPyInstallationManager pyInstallationManager
    )
        : base(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager) { }

    public override string Name => "dank-diffusion";
    public override string DisplayName { get; set; } = "Dank Diffusion";
    public override string Author => "mohnjiles";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => "https://github.com/LykosAI/StabilityMatrix/blob/main/LICENSE";
    public override string Blurb => "A dank interface for diffusion";
    public override string LaunchCommand => "test";
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override IReadOnlyDictionary<string, string> ExtraLaunchCommands =>
        new Dictionary<string, string> { ["test-config"] = "test-config" };

    public override Uri PreviewImageUri { get; }

    public override string OutputFolderName { get; }
    public override PackageDifficulty InstallerSortOrder { get; }

    public override Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public override Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotImplementedException();
    }

    public override Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        throw new NotImplementedException();
    }

    public override Task UpdateModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        throw new NotImplementedException();
    }

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        throw new NotImplementedException();
    }

    public override IEnumerable<TorchIndex> AvailableTorchIndices { get; }

    public override List<LaunchOptionDefinition> LaunchOptions { get; }

    public override Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders { get; }

    public override string MainBranch { get; }
}
