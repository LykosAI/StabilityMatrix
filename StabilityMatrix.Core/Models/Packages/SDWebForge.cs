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
    IPyInstallationManager pyInstallationManager
) : A3WebUI(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
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
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.ReallyRecommended;

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

        await venvRunner.PipInstall("--upgrade pip wheel joblib", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

        var requirements = new FilePath(installLocation, "requirements_versions.txt");
        var requirementsContent = await requirements
            .ReadAllTextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Collect all requirements.txt files from extensions-builtin subfolders
        var extensionsBuiltinDir = new DirectoryPath(installLocation, "extensions-builtin");
        if (extensionsBuiltinDir.Exists)
        {
            var requirementsFiles = extensionsBuiltinDir.EnumerateFiles(
                "requirements.txt",
                EnumerationOptionConstants.AllDirectories
            );

            foreach (var requirementsFile in requirementsFiles)
            {
                requirementsContent += await requirementsFile
                    .ReadAllTextAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var pipArgs = new PipInstallArgs();

        var isBlackwell =
            SettingsManager.Settings.PreferredGpu?.IsBlackwellGpu() ?? HardwareHelper.HasBlackwellGpu();
        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        pipArgs = pipArgs
            .WithTorch(isBlackwell ? string.Empty : "==2.3.1")
            .WithTorchVision(isBlackwell ? string.Empty : "==0.18.1")
            .WithTorchExtraIndex(
                torchVersion switch
                {
                    TorchIndex.Cpu => "cpu",
                    TorchIndex.Cuda when isBlackwell => "cu128",
                    TorchIndex.Cuda => "cu121",
                    TorchIndex.Rocm => "rocm5.7",
                    TorchIndex.Mps => "cpu",
                    _ => throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null),
                }
            );

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        pipArgs = new PipInstallArgs(
            "https://github.com/openai/CLIP/archive/d50d76daa670286dd6cacf3bcd80b5e4823fc8e1.zip"
        );
        pipArgs = pipArgs.WithParsedFromRequirementsTxt(requirementsContent, excludePattern: "torch");

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }
}
