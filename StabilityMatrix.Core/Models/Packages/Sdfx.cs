using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Config;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Sdfx>(Duplicate = DuplicateStrategy.Append)]
public class Sdfx(
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
    public override string Name => "sdfx";
    public override string DisplayName { get; set; } = "SDFX";
    public override string Author => "sdfxai";
    public override string Blurb =>
        "The ultimate no-code platform to build and share AI apps with beautiful UI.";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => "https://github.com/sdfxai/sdfx/blob/main/LICENSE";
    public override string LaunchCommand => "setup.py";
    public override Uri PreviewImageUri =>
        new("https://github.com/sdfxai/sdfx/raw/main/docs/static/screen-sdfx.png");
    public override string OutputFolderName => Path.Combine("data", "media", "output");

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        [TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.DirectMl, TorchIndex.Rocm, TorchIndex.Mps];

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Impossible;
    public override bool OfferInOneClickInstaller => false;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;
    public override List<LaunchOptionDefinition> LaunchOptions => [LaunchOptionDefinition.Extras];
    public override string Disclaimer => "This package may no longer receive updates from its author.";
    public override PackageType PackageType => PackageType.Legacy;

    public override SharedFolderLayout SharedFolderLayout =>
        new()
        {
            RelativeConfigPath = "sdfx.config.json",
            ConfigFileType = ConfigFileType.Json,
            Rules =
            [
                // Assuming JSON keys are top-level, adjust ConfigDocumentPaths if nested (e.g., "paths.models.checkpoints")
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    TargetRelativePaths = ["data/models/checkpoints"],
                    ConfigDocumentPaths = ["path.models.checkpoints"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Diffusers],
                    TargetRelativePaths = ["data/models/diffusers"],
                    ConfigDocumentPaths = ["path.models.diffusers"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["data/models/vae"],
                    ConfigDocumentPaths = ["path.models.vae"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    TargetRelativePaths = ["data/models/loras"],
                    ConfigDocumentPaths = ["path.models.loras"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Embeddings],
                    TargetRelativePaths = ["data/models/embeddings"],
                    ConfigDocumentPaths = ["path.models.embeddings"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Hypernetwork],
                    TargetRelativePaths = ["data/models/hypernetworks"],
                    ConfigDocumentPaths = ["path.models.hypernetworks"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes =
                    [
                        SharedFolderType.ESRGAN,
                        SharedFolderType.RealESRGAN,
                        SharedFolderType.SwinIR,
                    ],
                    TargetRelativePaths = ["data/models/upscale_models"],
                    ConfigDocumentPaths = ["path.models.upscale_models"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.TextEncoders],
                    TargetRelativePaths = ["data/models/clip"],
                    ConfigDocumentPaths = ["path.models.clip"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ClipVision],
                    TargetRelativePaths = ["data/models/clip_vision"],
                    ConfigDocumentPaths = ["path.models.clip_vision"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ControlNet, SharedFolderType.T2IAdapter],
                    TargetRelativePaths = ["data/models/controlnet"],
                    ConfigDocumentPaths = ["path.models.controlnet"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.GLIGEN],
                    TargetRelativePaths = ["data/models/gligen"],
                    ConfigDocumentPaths = ["path.models.gligen"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ApproxVAE],
                    TargetRelativePaths = ["data/models/vae_approx"],
                    ConfigDocumentPaths = ["path.models.vae_approx"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes =
                    [
                        SharedFolderType.IpAdapter,
                        SharedFolderType.IpAdapters15,
                        SharedFolderType.IpAdaptersXl,
                    ],
                    TargetRelativePaths = ["data/models/ipadapter"],
                    ConfigDocumentPaths = ["path.models.ipadapter"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.PromptExpansion],
                    TargetRelativePaths = ["data/models/prompt_expansion"],
                    ConfigDocumentPaths = ["path.models.prompt_expansion"],
                },
            ],
        };
    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = new[] { "data/media/output" } };
    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;

    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        [
            PackagePrerequisite.Python310,
            PackagePrerequisite.VcRedist,
            PackagePrerequisite.Git,
            PackagePrerequisite.Node,
        ];

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);
        venvRunner.UpdateEnvironmentVariables(GetEnvVars);

        progress?.Report(
            new ProgressReport(-1f, "Installing Package Requirements...", isIndeterminate: true)
        );

        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        var gpuArg = torchVersion switch
        {
            TorchIndex.Cuda => "--nvidia",
            TorchIndex.Rocm => "--amd",
            TorchIndex.DirectMl => "--directml",
            TorchIndex.Cpu => "--cpu",
            TorchIndex.Mps => "--mac",
            _ => throw new NotSupportedException($"Torch version {torchVersion} is not supported."),
        };

        await venvRunner
            .CustomInstall(["setup.py", "--install", gpuArg], onConsoleOutput)
            .ConfigureAwait(false);

        if (installedPackage.PipOverrides != null)
        {
            var pipArgs = new PipInstallArgs().WithUserOverrides(installedPackage.PipOverrides);
            await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(1, "Installed Package Requirements", isIndeterminate: false));
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var venvRunner = await SetupVenv(
                installLocation,
                pythonVersion: PyVersion.Parse(installedPackage.PythonVersion)
            )
            .ConfigureAwait(false);
        venvRunner.UpdateEnvironmentVariables(GetEnvVars);

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("Running on", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (!match.Success)
                return;

            WebUrl = match.Value;
            OnStartupComplete(WebUrl);
        }

        venvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), "--run", .. options.Arguments],
            HandleConsoleOutput,
            OnExit
        );

        // Cuz node was getting detached on process exit
        if (Compat.IsWindows)
        {
            ProcessTracker.AttachExitHandlerJobToProcess(venvRunner.Process);
        }
    }

    private ImmutableDictionary<string, string> GetEnvVars(ImmutableDictionary<string, string> env)
    {
        var pathBuilder = new EnvPathBuilder();

        if (env.TryGetValue("PATH", out var value))
        {
            pathBuilder.AddPath(value);
        }

        pathBuilder.AddPath(
            Compat.IsWindows
                ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                : Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs-18", "bin")
        );

        pathBuilder.AddPath(Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs-18"));

        return env.SetItem("PATH", pathBuilder.ToString());
    }
}
