using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Config;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, FooocusAPI>(Duplicate = DuplicateStrategy.Append)]
public class FooocusAPI(
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
    public override string Name => "Fooocus-API";
    public override string DisplayName { get; set; } = "Fooocus-API";
    public override string Author => "mrhan1993";

    public override string Blurb => "Fooocus is a rethinking of Stable Diffusion and Midjourney’s designs";
    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl => "https://github.com/mrhan1993/Fooocus-API/blob/main/LICENSE";
    public override string LaunchCommand => "main.py";
    public override PackageType PackageType => PackageType.Legacy;

    public override Uri PreviewImageUri =>
        new("https://github.com/mrhan1993/Fooocus-API/assets/820530/952c9777-8d57-4b7e-8bd3-f574d508ebee");

    public override List<LaunchOptionDefinition> LaunchOptions =>
        new()
        {
            new LaunchOptionDefinition
            {
                Name = "Preset",
                Type = LaunchOptionType.Bool,
                Options = { "--preset anime", "--preset realistic" },
            },
            new LaunchOptionDefinition
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                Description = "Sets the listen port",
                Options = { "--port" },
            },
            new LaunchOptionDefinition
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = { "--share" },
            },
            new LaunchOptionDefinition
            {
                Name = "Listen",
                Type = LaunchOptionType.String,
                Description = "Set the listen host",
                Options = { "--host" },
            },
            new LaunchOptionDefinition
            {
                Name = "Output Directory",
                Type = LaunchOptionType.String,
                Description = "Override the output directory",
                Options = { "--output-path" },
            },
            new LaunchOptionDefinition
            {
                Name = "Language",
                Type = LaunchOptionType.String,
                Description = "Change the language of the UI",
                Options = { "--language" },
            },
            new LaunchOptionDefinition
            {
                Name = "Auto-Launch",
                Type = LaunchOptionType.Bool,
                Options = { "--auto-launch" },
            },
            new LaunchOptionDefinition
            {
                Name = "Disable Image Log",
                Type = LaunchOptionType.Bool,
                Options = { "--disable-image-log" },
            },
            new LaunchOptionDefinition
            {
                Name = "Disable Analytics",
                Type = LaunchOptionType.Bool,
                Options = { "--disable-analytics" },
            },
            new LaunchOptionDefinition
            {
                Name = "Disable Preset Model Downloads",
                Type = LaunchOptionType.Bool,
                Options = { "--disable-preset-download" },
            },
            new LaunchOptionDefinition
            {
                Name = "Always Download Newer Models",
                Type = LaunchOptionType.Bool,
                Options = { "--always-download-new-model" },
            },
            new()
            {
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
                {
                    MemoryLevel.Low => "--always-low-vram",
                    MemoryLevel.Medium => "--always-normal-vram",
                    _ => null,
                },
                Options =
                {
                    "--always-high-vram",
                    "--always-normal-vram",
                    "--always-low-vram",
                    "--always-no-vram",
                },
            },
            new LaunchOptionDefinition
            {
                Name = "Use DirectML",
                Type = LaunchOptionType.Bool,
                Description = "Use pytorch with DirectML support",
                InitialValue = HardwareHelper.PreferDirectMLOrZluda(),
                Options = { "--directml" },
            },
            new LaunchOptionDefinition
            {
                Name = "Disable Xformers",
                Type = LaunchOptionType.Bool,
                InitialValue = !HardwareHelper.HasNvidiaGpu(),
                Options = { "--disable-xformers" },
            },
            LaunchOptionDefinition.Extras,
        };

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.Configuration, SharedFolderMethod.None };

    public override SharedFolderLayout SharedFolderLayout =>
        new()
        {
            RelativeConfigPath = "config.txt",
            ConfigFileType = ConfigFileType.Json,
            Rules =
            [
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    TargetRelativePaths = ["models/checkpoints"],
                    ConfigDocumentPaths = ["path_checkpoints"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Diffusers],
                    TargetRelativePaths = ["models/diffusers"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.TextEncoders],
                    TargetRelativePaths = ["models/clip"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.GLIGEN],
                    TargetRelativePaths = ["models/gligen"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ESRGAN],
                    TargetRelativePaths = ["models/upscale_models"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Hypernetwork],
                    TargetRelativePaths = ["models/hypernetworks"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Embeddings],
                    TargetRelativePaths = ["models/embeddings"],
                    ConfigDocumentPaths = ["path_embeddings"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["models/vae"],
                    ConfigDocumentPaths = ["path_vae"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ApproxVAE],
                    TargetRelativePaths = ["models/vae_approx"],
                    ConfigDocumentPaths = ["path_vae_approx"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora, SharedFolderType.LyCORIS],
                    TargetRelativePaths = ["models/loras"],
                    ConfigDocumentPaths = ["path_loras"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ClipVision],
                    TargetRelativePaths = ["models/clip_vision"],
                    ConfigDocumentPaths = ["path_clip_vision"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ControlNet],
                    TargetRelativePaths = ["models/controlnet"],
                    ConfigDocumentPaths = ["path_controlnet"],
                },
                new SharedFolderLayoutRule
                {
                    TargetRelativePaths = ["models/inpaint"],
                    ConfigDocumentPaths = ["path_inpaint"],
                },
                new SharedFolderLayoutRule
                {
                    TargetRelativePaths = ["models/prompt_expansion/fooocus_expansion"],
                    ConfigDocumentPaths = ["path_fooocus_expansion"],
                },
                new SharedFolderLayoutRule
                {
                    TargetRelativePaths = [OutputFolderName],
                    ConfigDocumentPaths = ["path_outputs"],
                },
            ],
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = new[] { "outputs" } };

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        new[] { TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.DirectMl, TorchIndex.Rocm, TorchIndex.Mps };

    public override string MainBranch => "main";

    public override bool ShouldIgnoreReleases => true;

    public override string OutputFolderName => "outputs";

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
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        var torchIndex = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        var isBlackwell =
            torchIndex is TorchIndex.Cuda
            && (SettingsManager.Settings.PreferredGpu?.IsBlackwellGpu() ?? HardwareHelper.HasBlackwellGpu());

        var config = new PipInstallConfig
        {
            // Pip version 24.1 deprecated numpy requirement spec used by torchsde 0.2.5
            PrePipInstallArgs = ["pip==23.3.2"],
            RequirementsFilePaths = ["requirements_versions.txt"],
            TorchVersion = isBlackwell ? "" : "==2.1.0",
            TorchvisionVersion = isBlackwell ? "" : "==0.16.0",
            CudaIndex = isBlackwell ? "cu128" : "cu121",
            RocmIndex = "rocm5.6",
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

            if (s.Text.Contains("Use the app with", StringComparison.OrdinalIgnoreCase))
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
