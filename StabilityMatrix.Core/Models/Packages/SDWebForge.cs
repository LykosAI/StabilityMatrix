using System.Text.Json.Nodes;
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
    public override string OutputFolderName => "output";
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Extras] = new[] { "output/extras-images" },
            [SharedOutputType.Saved] = new[] { "log/images" },
            [SharedOutputType.Img2Img] = new[] { "output/img2img-images" },
            [SharedOutputType.Text2Img] = new[] { "output/txt2img-images" },
            [SharedOutputType.Img2ImgGrids] = new[] { "output/img2img-grids" },
            [SharedOutputType.Text2ImgGrids] = new[] { "output/txt2img-grids" }
        };

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
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

    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Extras] = new[] { "output/extras-images" },
            [SharedOutputType.Saved] = new[] { "log/images" },
            [SharedOutputType.Img2Img] = new[] { "output/img2img-images" },
            [SharedOutputType.Text2Img] = new[] { "output/txt2img-images" },
            [SharedOutputType.Img2ImgGrids] = new[] { "output/img2img-grids" },
            [SharedOutputType.Text2ImgGrids] = new[] { "output/txt2img-grids" }
        };

    public override string OutputFolderName => "output";

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

        var venvPath = Path.Combine(installLocation, "venv");

        await using var venvRunner = new PyVenvRunner(venvPath);
        venvRunner.WorkingDirectory = installLocation;
        await venvRunner.Setup(true, onConsoleOutput).ConfigureAwait(false);
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

        var requirements = new FilePath(installLocation, "requirements_versions.txt");
        var pipArgs = new PipInstallArgs();
        if (torchVersion is TorchVersion.DirectMl)
        {
            pipArgs = pipArgs.WithTorchDirectML();
        }
        else
        {
            pipArgs = pipArgs
                .WithTorch("==2.1.2")
                .WithTorchVision("==0.16.2")
                .WithTorchExtraIndex(
                    torchVersion switch
                    {
                        TorchVersion.Cpu => "cpu",
                        TorchVersion.Cuda => "cu121",
                        TorchVersion.Rocm => "rocm5.6",
                        TorchVersion.Mps => "nightly/cpu",
                        _ => throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null)
                    }
                );
        }

        pipArgs = pipArgs.WithParsedFromRequirementsTxt(
            await requirements.ReadAllTextAsync().ConfigureAwait(false),
            excludePattern: "torch"
        );

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }
}
