using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages.Config;
using StabilityMatrix.Core.Models.Packages.Extensions;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, VladAutomatic>(Duplicate = DuplicateStrategy.Append)]
public class VladAutomatic(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public override string Name => "automatic";
    public override string DisplayName { get; set; } = "SD.Next";
    public override string Author => "vladmandic";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => "https://github.com/vladmandic/automatic/blob/master/LICENSE.txt";
    public override string Blurb => "Stable Diffusion implementation with advanced features and modern UI";
    public override string LaunchCommand => "launch.py";

    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/vladautomatic/preview.webp");
    public override bool ShouldIgnoreReleases => true;

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Expert;
    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;

    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        new[]
        {
            TorchIndex.Cpu,
            TorchIndex.Cuda,
            TorchIndex.DirectMl,
            TorchIndex.Ipex,
            TorchIndex.Rocm,
            TorchIndex.Zluda,
        };

    // https://github.com/vladmandic/automatic/blob/master/modules/shared.py#L324
    public override SharedFolderLayout SharedFolderLayout =>
        new()
        {
            RelativeConfigPath = "config.json",
            ConfigFileType = ConfigFileType.Json,
            Rules =
            [
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.StableDiffusion],
                    TargetRelativePaths = ["models/Stable-diffusion"],
                    ConfigDocumentPaths = ["ckpt_dir"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Diffusers],
                    TargetRelativePaths = ["models/Diffusers"],
                    ConfigDocumentPaths = ["diffusers_dir"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.VAE],
                    TargetRelativePaths = ["models/VAE"],
                    ConfigDocumentPaths = ["vae_dir"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Embeddings],
                    TargetRelativePaths = ["models/embeddings"],
                    ConfigDocumentPaths = ["embeddings_dir"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Hypernetwork],
                    TargetRelativePaths = ["models/hypernetworks"],
                    ConfigDocumentPaths = ["hypernetwork_dir"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Codeformer],
                    TargetRelativePaths = ["models/Codeformer"],
                    ConfigDocumentPaths = ["codeformer_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.GFPGAN],
                    TargetRelativePaths = ["models/GFPGAN"],
                    ConfigDocumentPaths = ["gfpgan_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.BSRGAN],
                    TargetRelativePaths = ["models/BSRGAN"],
                    ConfigDocumentPaths = ["bsrgan_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ESRGAN],
                    TargetRelativePaths = ["models/ESRGAN"],
                    ConfigDocumentPaths = ["esrgan_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.RealESRGAN],
                    TargetRelativePaths = ["models/RealESRGAN"],
                    ConfigDocumentPaths = ["realesrgan_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ScuNET],
                    TargetRelativePaths = ["models/ScuNET"],
                    ConfigDocumentPaths = ["scunet_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.SwinIR],
                    TargetRelativePaths = ["models/SwinIR"],
                    ConfigDocumentPaths = ["swinir_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.LDSR],
                    TargetRelativePaths = ["models/LDSR"],
                    ConfigDocumentPaths = ["ldsr_models_path"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.TextEncoders],
                    TargetRelativePaths = ["models/CLIP"],
                    ConfigDocumentPaths = ["clip_models_path"],
                }, // CLIP
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.Lora],
                    TargetRelativePaths = ["models/Lora"],
                    ConfigDocumentPaths = ["lora_dir"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.LyCORIS],
                    TargetRelativePaths = ["models/LyCORIS"],
                    ConfigDocumentPaths = ["lyco_dir"],
                },
                new SharedFolderLayoutRule
                {
                    SourceTypes = [SharedFolderType.ControlNet, SharedFolderType.T2IAdapter],
                    TargetRelativePaths = ["models/ControlNet"],
                    ConfigDocumentPaths = ["control_net_models_path"],
                }, // Combined ControlNet/T2I
            ],
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Text2Img] = new[] { "outputs/text" },
            [SharedOutputType.Img2Img] = new[] { "outputs/image" },
            [SharedOutputType.Extras] = new[] { "outputs/extras" },
            [SharedOutputType.Img2ImgGrids] = new[] { "outputs/grids" },
            [SharedOutputType.Text2ImgGrids] = new[] { "outputs/grids" },
            [SharedOutputType.Saved] = new[] { "outputs/save" },
        };

    public override string OutputFolderName => "outputs";
    public override IPackageExtensionManager ExtensionManager => new VladExtensionManager(this);

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
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
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
                {
                    MemoryLevel.Low => "--lowvram",
                    MemoryLevel.Medium => "--medvram",
                    _ => null,
                },
                Options = ["--lowvram", "--medvram"],
            },
            new()
            {
                Name = "Auto-Launch Web UI",
                Type = LaunchOptionType.Bool,
                Options = ["--autolaunch"],
            },
            new()
            {
                Name = "Force use of Intel OneAPI XPU backend",
                Type = LaunchOptionType.Bool,
                Options = ["--use-ipex"],
            },
            new()
            {
                Name = "Use DirectML if no compatible GPU is detected",
                Type = LaunchOptionType.Bool,
                Options = ["--use-directml"],
            },
            new()
            {
                Name = "Force use of Nvidia CUDA backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.HasNvidiaGpu(),
                Options = ["--use-cuda"],
            },
            new()
            {
                Name = "Force use of Intel OneAPI XPU backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.HasIntelGpu(),
                Options = ["--use-ipex"],
            },
            new()
            {
                Name = "Force use of AMD ROCm backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferRocm(),
                Options = ["--use-rocm"],
            },
            new()
            {
                Name = "Force use of ZLUDA backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferDirectMLOrZluda(),
                Options = ["--use-zluda"],
            },
            new()
            {
                Name = "CUDA Device ID",
                Type = LaunchOptionType.String,
                Options = ["--device-id"],
            },
            new()
            {
                Name = "API",
                Type = LaunchOptionType.Bool,
                Options = ["--api"],
            },
            new()
            {
                Name = "Debug Logging",
                Type = LaunchOptionType.Bool,
                Options = ["--debug"],
            },
            LaunchOptionDefinition.Extras,
        ];

    public override string MainBranch => "master";

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(new ProgressReport(-1f, "Installing package...", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        await venvRunner.PipInstall(["setuptools", "rich", "uv"]).ConfigureAwait(false);
        if (options.PythonOptions.PythonVersion is { Minor: < 12 })
        {
            venvRunner.UpdateEnvironmentVariables(env =>
                env.SetItem("SETUPTOOLS_USE_DISTUTILS", "setuptools")
            );
        }

        if (installedPackage.PipOverrides != null)
        {
            var pipArgs = new PipInstallArgs().WithUserOverrides(installedPackage.PipOverrides);
            await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        }

        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        switch (torchVersion)
        {
            // Run initial install
            case TorchIndex.Cuda:
                await venvRunner
                    .CustomInstall("launch.py --use-cuda --debug --test --uv", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchIndex.Rocm:
                await venvRunner
                    .CustomInstall("launch.py --use-rocm --debug --test --uv", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchIndex.DirectMl:
                await venvRunner
                    .CustomInstall("launch.py --use-directml --debug --test --uv", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchIndex.Zluda:
                await venvRunner
                    .CustomInstall("launch.py --use-zluda --debug --test --uv", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchIndex.Ipex:
                await venvRunner
                    .CustomInstall("launch.py --use-ipex --debug --test --uv", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            default:
                // CPU
                await venvRunner
                    .CustomInstall("launch.py --debug --test --uv", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
        }

        progress?.Report(new ProgressReport(1f, isIndeterminate: false));
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

        if (PyVersion.Parse(installedPackage.PythonVersion) is { Minor: < 12 })
        {
            VenvRunner.UpdateEnvironmentVariables(env =>
                env.SetItem("SETUPTOOLS_USE_DISTUTILS", "setuptools")
            );
        }

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);
            if (s.Text.Contains("Local URL", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (match.Success)
                {
                    WebUrl = match.Value;
                    OnStartupComplete(WebUrl);
                }
            }
        }

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), "--uv", .. options.Arguments],
            HandleConsoleOutput,
            OnExit
        );
    }

    public override async Task<InstalledPackageVersion> Update(
        string installLocation,
        InstalledPackage installedPackage,
        UpdatePackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var baseUpdateResult = await base.Update(
                installLocation,
                installedPackage,
                options,
                progress,
                onConsoleOutput,
                cancellationToken
            )
            .ConfigureAwait(false);

        await using var venvRunner = await SetupVenvPure(
                installedPackage.FullPath!.Unwrap(),
                pythonVersion: PyVersion.Parse(installedPackage.PythonVersion)
            )
            .ConfigureAwait(false);

        await venvRunner.CustomInstall("launch.py --upgrade --test", onConsoleOutput).ConfigureAwait(false);

        try
        {
            var result = await PrerequisiteHelper
                .GetGitOutput(["rev-parse", "HEAD"], installedPackage.FullPath)
                .EnsureSuccessExitCode()
                .ConfigureAwait(false);

            return new InstalledPackageVersion
            {
                InstalledBranch = options.VersionOptions.BranchName,
                InstalledCommitSha = result
                    .StandardOutput?.Replace(Environment.NewLine, "")
                    .Replace("\n", ""),
                IsPrerelease = false,
            };
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Could not get current git hash, continuing with update");
        }
        finally
        {
            progress?.Report(
                new ProgressReport(
                    1f,
                    message: "Update Complete",
                    isIndeterminate: false,
                    type: ProgressType.Update
                )
            );
        }

        return baseUpdateResult;
    }

    private class VladExtensionManager(VladAutomatic package)
        : GitPackageExtensionManager(package.PrerequisiteHelper)
    {
        public override string RelativeInstallDirectory => "extensions";

        public override IEnumerable<ExtensionManifest> DefaultManifests =>
            [new ExtensionManifest(new Uri("https://vladmandic.github.io/sd-data/pages/extensions.json"))];

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
                var jsonManifest = JsonSerializer.Deserialize<IEnumerable<VladExtensionItem>>(
                    content,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );

                return jsonManifest?.Select(entry => new PackageExtension
                    {
                        Title = entry.Name,
                        Author = entry.Long?.Split('/').FirstOrDefault() ?? "Unknown",
                        Reference = entry.Url,
                        Files = [entry.Url],
                        Description = entry.Description,
                        InstallType = "git-clone",
                    }) ?? Enumerable.Empty<PackageExtension>();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get extensions from manifest");
                return Enumerable.Empty<PackageExtension>();
            }
        }
    }
}
