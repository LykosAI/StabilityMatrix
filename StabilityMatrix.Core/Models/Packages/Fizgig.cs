using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Fizgig>(Duplicate = DuplicateStrategy.Append)]
public class Fizgig(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
)
    : BaseGitPackage(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    public override string Name => "Fizgig";
    public override string DisplayName { get; set; } = "Fizgig";
    public override string Author => "shootthesound";

    public override string Blurb =>
        "LoRA training studio for Flux 2 Klein 9B, Krea 2, MiniMax H3 and Qwen Image 2.1 — train, profile, repair and extract";

    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/shootthesound/Fizgig/blob/master/LICENSE";

    // NOT launch.pyw: that launcher re-spawns itself under venv/Scripts/pythonw.exe and exits,
    // which would drop the process we track (no console output, no working Stop button) and
    // leave the GUI orphaned. lora_trainer_gui.py has a standalone main() and is what upstream's
    // run_fizgig.sh invokes directly.
    public override string LaunchCommand => "lora_trainer_gui.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/shootthesound/Fizgig/blob/master/icon.png?raw=true");

    public override string MainBranch => "master";
    public override PackageType PackageType => PackageType.SdTraining;
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;
    public override bool OfferInOneClickInstaller => false;
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];

    public override TorchIndex GetRecommendedTorchVersion() => TorchIndex.Cuda;

    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;

    // Tkinter for the GUI itself; VcBuildTools for triton / torch.compile's inductor backend,
    // which the Compile Blocks speedup needs on Windows.
    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.Tkinter, PackagePrerequisite.VcBuildTools]);

    public override List<LaunchOptionDefinition> LaunchOptions => [LaunchOptionDefinition.Extras];

    // Trained LoRAs, not images.
    public override string OutputFolderName => string.Empty;
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders => null;

    /// <summary>
    /// Defaults to None, matching the other trainers. Opting in to Symlink (Package Manager ->
    /// ... -> Shared Model Strategy) junctions output_loras into the shared Lora folder, so a
    /// freshly trained LoRA is immediately visible to ComfyUI and friends.
    /// </summary>
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.None, SharedFolderMethod.Symlink];

    /// <remarks>
    /// Only output_loras is mapped. Fizgig's models/ directory deliberately flattens every
    /// weight it downloads into one folder — DiTs, text encoders, VAEs, turbo LoRAs and training
    /// adapters all land there as bare filenames (see src/fizgig/scripts/fetch_models.py) — and
    /// junctions are directory-level, so there is no way to fan that single directory out to
    /// DiffusionModels/TextEncoders/VAE without them colliding on the same target path.
    /// </remarks>
    public override SharedFolderLayout SharedFolderLayout =>
        new()
        {
            Rules =
            [
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora],
                    TargetRelativePaths = ["output_loras"],
                },
            ],
        };

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

        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        // hqq ships as an sdist whose setup.py kicks off a CUDA kernel build during egg_info
        // unless DISABLE_CUDA is set. Fizgig only uses its pure-PyTorch path, and its own
        // requirements.txt warns never to install that line without this.
        venvRunner.UpdateEnvironmentVariables(env => env.SetItem("DISABLE_CUDA", "1"));

        var config = new PipInstallConfig
        {
            RequirementsFilePaths = ["requirements.txt"],
            // Drop the torch pins and the cu128 index line from the file so the torch install
            // below is the single source of truth for which build lands in the venv. The pattern
            // is anchored against the whole entry by the caller, so the version specifier has to
            // be matched too - the default pattern only catches bare, unpinned names.
            RequirementsExcludePattern =
                @"(--extra-index-url.*|(torch|torchvision|torchaudio|xformers)([=<>!~].*)?)",
            TorchVersion = "==2.10.0",
            TorchvisionVersion = "==0.25.0",
            // torch 2.10 pairs with cu128 here; SM's default cu130 has no matching wheels.
            CudaIndex = "cu128",
        };

        await StandardPipInstallProcessAsync(
                venvRunner,
                options,
                installedPackage,
                config,
                onConsoleOutput,
                progress,
                cancellationToken
            )
            .ConfigureAwait(false);

        venvRunner.UpdateEnvironmentVariables(env => env.Remove("DISABLE_CUDA"));
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);

        // Desktop Tkinter app - there is no local URL to wait for, so startup is complete
        // as soon as the process is up.
        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), .. options.Arguments],
            onConsoleOutput,
            OnExit
        );
    }
}
