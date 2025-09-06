using System.Text.RegularExpressions;
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

[RegisterSingleton<BasePackage, FluxGym>(Duplicate = DuplicateStrategy.Append)]
public class FluxGym(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
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

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.None };

    public override SharedFolderLayout SharedFolderLayout =>
        new()
        {
            Rules =
            [
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.TextEncoders],
                    TargetRelativePaths = ["models/clip"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.DiffusionModels],
                    TargetRelativePaths = ["models/unet"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["models/vae"],
                },
            ],
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders => null;
    public override IEnumerable<TorchIndex> AvailableTorchIndices => new[] { TorchIndex.Cuda };
    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;
    public override string OutputFolderName => string.Empty;
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
        progress?.Report(new ProgressReport(-1f, "Cloning / updating sd-scripts", isIndeterminate: true));
        // check if sd-scripts is already installed - if so: pull, else: clone
        if (Directory.Exists(Path.Combine(installLocation, "sd-scripts")))
        {
            await PrerequisiteHelper
                .RunGit(["pull"], onConsoleOutput, Path.Combine(installLocation, "sd-scripts"))
                .ConfigureAwait(false);
        }
        else
        {
            await PrerequisiteHelper
                .RunGit(
                    ["clone", "-b", "sd3", "https://github.com/kohya-ss/sd-scripts"],
                    onConsoleOutput,
                    installLocation
                )
                .ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        var isLegacyNvidiaGpu =
            SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() ?? HardwareHelper.HasLegacyNvidiaGpu();

        var config = new PipInstallConfig
        {
            RequirementsFilePaths = ["sd-scripts/requirements.txt", "requirements.txt"],
            RequirementsExcludePattern =
                "(diffusers\\[torch\\]==0.32.1|torch|torchvision|torchaudio|xformers|bitsandbytes|-e\\s\\.)",
            TorchaudioVersion = " ",
            CudaIndex = isLegacyNvidiaGpu ? "cu126" : "cu128",
            ExtraPipArgs = ["bitsandbytes>=0.46.0"],
            PostInstallPipArgs = ["diffusers[torch]==0.32.1"],
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

        await venvRunner.PipInstall(["-e", "./sd-scripts"], onConsoleOutput).ConfigureAwait(false);
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

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (s.Text.Contains("Running on local URL", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);

                if (match.Success)
                {
                    WebUrl = match.Value;
                }
                OnStartupComplete(WebUrl);
            }
        }

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), .. options.Arguments],
            HandleConsoleOutput,
            OnExit
        );
    }
}
