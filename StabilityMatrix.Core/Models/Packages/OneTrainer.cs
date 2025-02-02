using System.Diagnostics;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, OneTrainer>(Duplicate = DuplicateStrategy.Append)]
public class OneTrainer(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "OneTrainer";
    public override string DisplayName { get; set; } = "OneTrainer";
    public override string Author => "Nerogar";
    public override string Blurb =>
        "OneTrainer is a one-stop solution for all your stable diffusion training needs";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => "https://github.com/Nerogar/OneTrainer/blob/master/LICENSE.txt";
    public override string LaunchCommand => "scripts/train_ui.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/Nerogar/OneTrainer/blob/master/resources/icons/icon.png?raw=true");

    public override string OutputFolderName => string.Empty;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda, TorchIndex.Rocm];
    public override PackageType PackageType => PackageType.SdTraining;
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.None };
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Nightmare;
    public override bool OfferInOneClickInstaller => false;
    public override bool ShouldIgnoreReleases => true;
    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.Tkinter]);

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements", isIndeterminate: true));
        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        var requirementsFileName = torchVersion switch
        {
            TorchIndex.Cuda => "requirements-cuda.txt",
            TorchIndex.Rocm => "requirements-rocm.txt",
            _ => "requirements-default.txt"
        };

        await venvRunner.PipInstall(["-r", requirementsFileName], onConsoleOutput).ConfigureAwait(false);
        await venvRunner.PipInstall(["-r", "requirements-global.txt"], onConsoleOutput).ConfigureAwait(false);

        if (installedPackage.PipOverrides != null)
        {
            var pipArgs = new PipInstallArgs().WithUserOverrides(installedPackage.PipOverrides);
            await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        }
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await SetupVenv(installLocation).ConfigureAwait(false);

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), ..options.Arguments],
            onConsoleOutput,
            OnExit
        );
    }

    public override List<LaunchOptionDefinition> LaunchOptions => [LaunchOptionDefinition.Extras];
    public override Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders { get; }
    public override string MainBranch => "master";
}
