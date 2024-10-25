using System.Text.RegularExpressions;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class FluxGym(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
)
    : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper),
        ISharedFolderLayoutPackage
{
    public override string Name => "FluxGym";
    public override string DisplayName { get; set; } = "FluxGym";
    public override string Author => "cocktailpeanut";

    public override string Blurb => "Dead simple FLUX LoRA training UI with LOW VRAM support";

    public override string LicenseType => "N/A";
    public override string LicenseUrl => "";
    public override string LaunchCommand => "app.py";

    public override Uri PreviewImageUri =>
        new("https://raw.githubusercontent.com/cocktailpeanut/fluxgym/main/screenshot.png");

    public override List<LaunchOptionDefinition> LaunchOptions => [LaunchOptionDefinition.Extras];

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.Configuration, SharedFolderMethod.None };

    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        ((ISharedFolderLayoutPackage)this).LegacySharedFolders;

    public virtual SharedFolderLayout SharedFolderLayout =>
        new()
        {
            RelativeConfigPath = "config.txt",
            Rules =
            [
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.CLIP],
                    TargetRelativePaths = ["models/clip"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Unet],
                    TargetRelativePaths = ["models/unet"]
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["models/vae"],
                    ConfigDocumentPaths = ["path_vae"]
                },
                new SharedFolderLayoutRule
                {
                    TargetRelativePaths = [OutputFolderName],
                    ConfigDocumentPaths = ["path_outputs"]
                }
            ]
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = new[] { "outputs" } };

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        new[] { TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.DirectMl, TorchIndex.Rocm, TorchIndex.Mps };

    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;
    public override string OutputFolderName => "outputs";
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();
    public override PackageType PackageType => PackageType.SdTraining;
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Simple;

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(new ProgressReport(-1f, "Cloning sd-scripts", isIndeterminate: true));
        await prerequisiteHelper
            .RunGit(
                ["clone", "-b", "sd3", "https://github.com/kohya-ss/sd-scripts"],
                onConsoleOutput,
                installLocation
            )
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(-1f, "Installing sd-scripts requirements", isIndeterminate: true)
        );
        var sdsRequirements = new FilePath(installLocation, "sd-scripts", "requirements.txt");
        var sdsPipArgs = new PipInstallArgs().WithParsedFromRequirementsTxt(
            await sdsRequirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
            "torch"
        );
        await venvRunner.PipInstall(sdsPipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements", isIndeterminate: true));
        var requirements = new FilePath(installLocation, "requirements.txt");
        var pipArgs = new PipInstallArgs()
            .AddArg("--pre")
            .WithTorch()
            .WithTorchVision()
            .WithTorchAudio()
            .WithTorchExtraIndex("cu121")
            .WithParsedFromRequirementsTxt(
                await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
                "torch"
            );

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
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
}
