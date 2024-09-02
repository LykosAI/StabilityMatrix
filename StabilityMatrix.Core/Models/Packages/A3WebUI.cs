using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class A3WebUI(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public override string Name => "stable-diffusion-webui";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI";
    public override string Author => "AUTOMATIC1111";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl =>
        "https://github.com/AUTOMATIC1111/stable-diffusion-webui/blob/master/LICENSE.txt";
    public override string Blurb => "A browser interface based on Gradio library for Stable Diffusion";
    public override string LaunchCommand => "launch.py";
    public override Uri PreviewImageUri =>
        new("https://github.com/AUTOMATIC1111/stable-diffusion-webui/raw/master/screenshot.png");
    public string RelativeArgsDefinitionScriptPath => "modules.cmd_args";

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Recommended;

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    // From https://github.com/AUTOMATIC1111/stable-diffusion-webui/tree/master/models
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
            [SharedFolderType.ESRGAN] = new[] { "models/ESRGAN" },
            [SharedFolderType.GFPGAN] = new[] { "models/GFPGAN" },
            [SharedFolderType.RealESRGAN] = new[] { "models/RealESRGAN" },
            [SharedFolderType.SwinIR] = new[] { "models/SwinIR" },
            [SharedFolderType.Lora] = new[] { "models/Lora" },
            [SharedFolderType.LyCORIS] = new[] { "models/LyCORIS" },
            [SharedFolderType.ApproxVAE] = new[] { "models/VAE-approx" },
            [SharedFolderType.VAE] = new[] { "models/VAE" },
            [SharedFolderType.DeepDanbooru] = new[] { "models/deepbooru" },
            [SharedFolderType.Karlo] = new[] { "models/karlo" },
            [SharedFolderType.TextualInversion] = new[] { "embeddings" },
            [SharedFolderType.Hypernetwork] = new[] { "models/hypernetworks" },
            [SharedFolderType.ControlNet] = new[] { "models/controlnet/ControlNet" },
            [SharedFolderType.Codeformer] = new[] { "models/Codeformer" },
            [SharedFolderType.LDSR] = new[] { "models/LDSR" },
            [SharedFolderType.AfterDetailer] = new[] { "models/adetailer" },
            [SharedFolderType.T2IAdapter] = new[] { "models/controlnet/T2IAdapter" },
            [SharedFolderType.IpAdapter] = new[] { "models/controlnet/IpAdapter" },
            [SharedFolderType.InvokeIpAdapters15] = new[] { "models/controlnet/DiffusersIpAdapters" },
            [SharedFolderType.InvokeIpAdaptersXl] = new[] { "models/controlnet/DiffusersIpAdaptersXL" },
            [SharedFolderType.SVD] = new[] { "models/svd" },
            [SharedFolderType.CLIP] = new[] { "models/text_encoder" }
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Extras] = new[] { "outputs/extras-images" },
            [SharedOutputType.Saved] = new[] { "log/images" },
            [SharedOutputType.Img2Img] = new[] { "outputs/img2img-images" },
            [SharedOutputType.Text2Img] = new[] { "outputs/txt2img-images" },
            [SharedOutputType.Img2ImgGrids] = new[] { "outputs/img2img-grids" },
            [SharedOutputType.Text2ImgGrids] = new[] { "outputs/txt2img-grids" },
            [SharedOutputType.SVD] = new[] { "outputs/svd" }
        };

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = ["--server-name"]
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = ["--port"]
            },
            new LaunchOptionDefinition
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = { "--share" }
            },
            new()
            {
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
                {
                    MemoryLevel.Low => "--lowvram",
                    MemoryLevel.Medium => "--medvram",
                    _ => null
                },
                Options = ["--lowvram", "--medvram", "--medvram-sdxl"]
            },
            new()
            {
                Name = "Xformers",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.HasNvidiaGpu(),
                Options = ["--xformers"]
            },
            new()
            {
                Name = "API",
                Type = LaunchOptionType.Bool,
                InitialValue = true,
                Options = ["--api"]
            },
            new()
            {
                Name = "Auto Launch Web UI",
                Type = LaunchOptionType.Bool,
                InitialValue = false,
                Options = ["--autolaunch"]
            },
            new()
            {
                Name = "Skip Torch CUDA Check",
                Type = LaunchOptionType.Bool,
                InitialValue = !HardwareHelper.HasNvidiaGpu(),
                Options = ["--skip-torch-cuda-test"]
            },
            new()
            {
                Name = "Skip Python Version Check",
                Type = LaunchOptionType.Bool,
                InitialValue = true,
                Options = ["--skip-python-version-check"]
            },
            new()
            {
                Name = "No Half",
                Type = LaunchOptionType.Bool,
                Description = "Do not switch the model to 16-bit floats",
                InitialValue =
                    HardwareHelper.PreferRocm() || HardwareHelper.PreferDirectML() || Compat.IsMacOS,
                Options = ["--no-half"]
            },
            new()
            {
                Name = "Skip SD Model Download",
                Type = LaunchOptionType.Bool,
                InitialValue = false,
                Options = ["--no-download-sd-model"]
            },
            new()
            {
                Name = "Skip Install",
                Type = LaunchOptionType.Bool,
                Options = ["--skip-install"]
            },
            LaunchOptionDefinition.Extras
        ];

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.None };

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        new[] { TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.Rocm, TorchIndex.Mps };

    public override string MainBranch => "master";

    public override string OutputFolderName => "outputs";

    public override IPackageExtensionManager ExtensionManager => new A3WebUiExtensionManager(this);

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
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        var requirements = new FilePath(installLocation, "requirements_versions.txt");
        var pipArgs = new PipInstallArgs()
            .WithTorch("==2.1.2")
            .WithTorchVision("==0.16.2")
            .WithTorchExtraIndex(
                options.PythonOptions.TorchIndex switch
                {
                    TorchIndex.Cpu => "cpu",
                    TorchIndex.Cuda => "cu121",
                    TorchIndex.Rocm => "rocm5.6",
                    TorchIndex.Mps => "cpu",
                    _ => throw new NotSupportedException($"Unsupported torch version: {torchVersion}")
                }
            )
            .WithParsedFromRequirementsTxt(
                await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
                excludePattern: "torch"
            );

        if (torchVersion == TorchIndex.Cuda)
        {
            pipArgs = pipArgs.WithXFormers("==0.0.23.post1");
        }

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Updating configuration", isIndeterminate: true));

        // Create and add {"show_progress_type": "TAESD"} to config.json
        // Only add if the file doesn't exist
        var configPath = Path.Combine(installLocation, "config.json");
        if (!File.Exists(configPath))
        {
            var config = new JsonObject { { "show_progress_type", "TAESD" } };
            await File.WriteAllTextAsync(configPath, config.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
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

        VenvRunner.RunDetached(
            [
                Path.Combine(installLocation, options.Command ?? LaunchCommand),
                ..options.Arguments,
                ..ExtraLaunchArguments
            ],
            HandleConsoleOutput,
            OnExit
        );
    }

    public override IReadOnlyList<string> ExtraLaunchArguments =>
        settingsManager.IsLibraryDirSet ? ["--gradio-allowed-path", settingsManager.ImagesDirectory] : [];

    private class A3WebUiExtensionManager(A3WebUI package)
        : GitPackageExtensionManager(package.PrerequisiteHelper)
    {
        public override string RelativeInstallDirectory => "extensions";

        public override IEnumerable<ExtensionManifest> DefaultManifests =>
            [
                new ExtensionManifest(
                    new Uri(
                        "https://raw.githubusercontent.com/AUTOMATIC1111/stable-diffusion-webui-extensions/master/index.json"
                    )
                )
            ];

        public override async Task<IEnumerable<PackageExtension>> GetManifestExtensionsAsync(
            ExtensionManifest manifest,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                // Get json
                var content = await package
                    .DownloadService.GetContentAsync(manifest.Uri.ToString(), cancellationToken)
                    .ConfigureAwait(false);

                // Parse json
                var jsonManifest = JsonSerializer.Deserialize<A1111ExtensionManifest>(
                    content,
                    A1111ExtensionManifestSerializerContext.Default.Options
                );

                return jsonManifest?.GetPackageExtensions() ?? Enumerable.Empty<PackageExtension>();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get extensions from manifest");
                return Enumerable.Empty<PackageExtension>();
            }
        }
    }
}
