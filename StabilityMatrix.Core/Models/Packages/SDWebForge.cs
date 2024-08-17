using StabilityMatrix.Core.Attributes;
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
public class SDWebForge(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : A3WebUI(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "stable-diffusion-webui-forge";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI Forge";
    public override string Author => "lllyasviel";

    public override string Blurb =>
        "Stable Diffusion WebUI Forge is a platform on top of Stable Diffusion WebUI (based on Gradio) to make development easier, optimize resource management, and speed up inference.";

    public override string LicenseUrl =>
        "https://github.com/lllyasviel/stable-diffusion-webui-forge/blob/main/LICENSE.txt";

    public override Uri PreviewImageUri =>
        new(
            "https://github.com/lllyasviel/stable-diffusion-webui-forge/assets/19834515/ca5e05ed-bd86-4ced-8662-f41034648e8c"
        );

    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;
    public override IPackageExtensionManager ExtensionManager => null;
<<<<<<< HEAD
    public override string OutputFolderName => "output";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.ReallyRecommended;
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Extras] = new[] { "output/extras-images" },
            [SharedOutputType.Saved] = new[] { "log/images" },
            [SharedOutputType.Img2Img] = new[] { "output/img2img-images" },
            [SharedOutputType.Text2Img] = new[] { "output/txt2img-images" },
            [SharedOutputType.Img2ImgGrids] = new[] { "output/img2img-grids" },
            [SharedOutputType.Text2ImgGrids] = new[] { "output/txt2img-grids" },
            [SharedOutputType.SVD] = new[] { "output/svd" }
        };
=======
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Simple;
>>>>>>> db05d8b9 (Merge pull request #784 from ionite34/fix-forge-output-sharing)

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new LaunchOptionDefinition
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = ["--server-name"]
            },
            new LaunchOptionDefinition
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
            new LaunchOptionDefinition
            {
                Name = "Pin Shared Memory",
                Type = LaunchOptionType.Bool,
                Options = { "--pin-shared-memory" }
            },
            new LaunchOptionDefinition
            {
                Name = "CUDA Malloc",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-malloc" }
            },
            new LaunchOptionDefinition
            {
                Name = "CUDA Stream",
                Type = LaunchOptionType.Bool,
                Options = { "--cuda-stream" }
            },
            new LaunchOptionDefinition
            {
                Name = "Always Offload from VRAM",
                Type = LaunchOptionType.Bool,
                Options = ["--always-offload-from-vram"]
            },
            new LaunchOptionDefinition
            {
                Name = "Always GPU",
                Type = LaunchOptionType.Bool,
                Options = ["--always-gpu"]
            },
            new LaunchOptionDefinition
            {
                Name = "Always CPU",
                Type = LaunchOptionType.Bool,
                Options = ["--always-cpu"]
            },
            new LaunchOptionDefinition
            {
                Name = "Use DirectML",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferDirectML(),
                Options = ["--directml"]
            },
            new LaunchOptionDefinition
            {
                Name = "Skip Torch CUDA Test",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS,
                Options = ["--skip-torch-cuda-test"]
            },
            new LaunchOptionDefinition
            {
                Name = "No half-precision VAE",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS,
                Options = ["--no-half-vae"]
            },
            LaunchOptionDefinition.Extras
        ];

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[]
        {
            TorchVersion.Cpu,
            TorchVersion.Cuda,
            TorchVersion.DirectMl,
            TorchVersion.Rocm,
            TorchVersion.Mps
        };

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        SharedFolderMethod selectedSharedFolderMethod,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));

        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

        var requirements = new FilePath(installLocation, "requirements_versions.txt");
        var requirementsContent = await requirements.ReadAllTextAsync().ConfigureAwait(false);

        var pipArgs = new PipInstallArgs("setuptools==69.5.1");
        if (torchVersion is TorchVersion.DirectMl)
        {
            pipArgs = pipArgs.WithTorchDirectML();
        }
        else
        {
            pipArgs = pipArgs
                .WithTorch("==2.3.1")
                .WithTorchVision("==0.18.1")
                .WithTorchExtraIndex(
                    torchVersion switch
                    {
                        TorchVersion.Cpu => "cpu",
                        TorchVersion.Cuda => "cu121",
                        TorchVersion.Rocm => "rocm5.6",
                        TorchVersion.Mps => "cpu",
                        _ => throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null)
                    }
                );
        }

        pipArgs = pipArgs.WithParsedFromRequirementsTxt(requirementsContent, excludePattern: "torch");

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }
}
