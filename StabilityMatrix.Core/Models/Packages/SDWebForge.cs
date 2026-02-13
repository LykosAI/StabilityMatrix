using System.Text;
using Injectio.Attributes;
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

[RegisterSingleton<BasePackage, SDWebForge>(Duplicate = DuplicateStrategy.Append)]
public class SDWebForge(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
)
    : A3WebUI(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    public override string Name => "stable-diffusion-webui-forge";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI Forge";
    public override string Author => "lllyasviel";

    public override string Blurb =>
        "Stable Diffusion WebUI Forge is a platform on top of Stable Diffusion WebUI (based on Gradio) to make development easier, optimize resource management, and speed up inference.";

    public override string LicenseUrl =>
        "https://github.com/lllyasviel/stable-diffusion-webui-forge/blob/main/LICENSE.txt";

    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/sdwebforge/preview.webp");

    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;
    public override IPackageExtensionManager ExtensionManager => null;
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Simple;
    public override PackageType PackageType => PackageType.Legacy;

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = ["--server-name"],
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = ["--port"],
            },
            new()
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = { "--share" },
            },
            new()
            {
                Name = "Pin Shared Memory",
                Type = LaunchOptionType.Bool,
                Options = { "--pin-shared-memory" },
                InitialValue =
                    HardwareHelper.HasNvidiaGpu()
                    && (
                        SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() is false
                        || !HardwareHelper.HasLegacyNvidiaGpu()
                    ),
            },
            new()
            {
                Name = "CUDA Malloc",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-malloc" },
                InitialValue =
                    HardwareHelper.HasNvidiaGpu()
                    && (
                        SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() is false
                        || !HardwareHelper.HasLegacyNvidiaGpu()
                    ),
            },
            new()
            {
                Name = "CUDA Stream",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-stream" },
                InitialValue =
                    HardwareHelper.HasNvidiaGpu()
                    && (
                        SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() is false
                        || !HardwareHelper.HasLegacyNvidiaGpu()
                    ),
            },
            new()
            {
                Name = "Skip Install",
                Type = LaunchOptionType.Bool,
                InitialValue = true,
                Options = ["--skip-install"],
            },
            new()
            {
                Name = "Always Offload from VRAM",
                Type = LaunchOptionType.Bool,
                Options = ["--always-offload-from-vram"],
            },
            new()
            {
                Name = "Always GPU",
                Type = LaunchOptionType.Bool,
                Options = ["--always-gpu"],
            },
            new()
            {
                Name = "Always CPU",
                Type = LaunchOptionType.Bool,
                Options = ["--always-cpu"],
            },
            new()
            {
                Name = "Skip Torch CUDA Test",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS,
                Options = ["--skip-torch-cuda-test"],
            },
            new()
            {
                Name = "No half-precision VAE",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS,
                Options = ["--no-half-vae"],
            },
            LaunchOptionDefinition.Extras,
        ];

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        [TorchIndex.Cpu, TorchIndex.Cuda, TorchIndex.Rocm, TorchIndex.Mps];

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

        // Dynamically discover all requirements files
        var requirementsPaths = new List<string> { "requirements_versions.txt" };
        var extensionsBuiltinDir = new DirectoryPath(installLocation, "extensions-builtin");
        if (extensionsBuiltinDir.Exists)
        {
            requirementsPaths.AddRange(
                extensionsBuiltinDir
                    .EnumerateFiles("requirements.txt", EnumerationOptionConstants.AllDirectories)
                    .Select(f => Path.GetRelativePath(installLocation, f.ToString()))
            );
        }

        var torchIndex = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        var isBlackwell =
            torchIndex is TorchIndex.Cuda
            && (SettingsManager.Settings.PreferredGpu?.IsBlackwellGpu() ?? HardwareHelper.HasBlackwellGpu());

        var isAmd = torchIndex is TorchIndex.Rocm;

        var config = new PipInstallConfig
        {
            PrePipInstallArgs = ["joblib", "setuptools<82"],
            RequirementsFilePaths = requirementsPaths,
            TorchVersion = "",
            TorchvisionVersion = "",
            CudaIndex = isBlackwell ? "cu128" : "cu126",
            RocmIndex = "rocm7.1",
            ExtraPipArgs =
            [
                "https://github.com/openai/CLIP/archive/d50d76daa670286dd6cacf3bcd80b5e4823fc8e1.zip",
            ],
            PostInstallPipArgs = ["numpy==1.26.4", "setuptools<82"],
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

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }
}
