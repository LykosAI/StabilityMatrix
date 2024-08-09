using System.Diagnostics;
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
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class VladAutomatic(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public override string Name => "automatic";
    public override string DisplayName { get; set; } = "SD.Next";
    public override string Author => "vladmandic";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => "https://github.com/vladmandic/automatic/blob/master/LICENSE.txt";
    public override string Blurb => "Stable Diffusion implementation with advanced features and modern UI";
    public override string LaunchCommand => "launch.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/vladmandic/automatic/raw/master/html/screenshot-modernui.jpg");
    public override bool ShouldIgnoreReleases => true;

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Expert;

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[]
        {
            TorchVersion.Cpu,
            TorchVersion.Cuda,
            TorchVersion.DirectMl,
            TorchVersion.Ipex,
            TorchVersion.Rocm,
            TorchVersion.Zluda,
        };

    // https://github.com/vladmandic/automatic/blob/master/modules/shared.py#L324
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
            [SharedFolderType.Diffusers] = new[] { "models/Diffusers" },
            [SharedFolderType.VAE] = new[] { "models/VAE" },
            [SharedFolderType.TextualInversion] = new[] { "models/embeddings" },
            [SharedFolderType.Hypernetwork] = new[] { "models/hypernetworks" },
            [SharedFolderType.Codeformer] = new[] { "models/Codeformer" },
            [SharedFolderType.GFPGAN] = new[] { "models/GFPGAN" },
            [SharedFolderType.BSRGAN] = new[] { "models/BSRGAN" },
            [SharedFolderType.ESRGAN] = new[] { "models/ESRGAN" },
            [SharedFolderType.RealESRGAN] = new[] { "models/RealESRGAN" },
            [SharedFolderType.ScuNET] = new[] { "models/ScuNET" },
            [SharedFolderType.SwinIR] = new[] { "models/SwinIR" },
            [SharedFolderType.LDSR] = new[] { "models/LDSR" },
            [SharedFolderType.CLIP] = new[] { "models/CLIP" },
            [SharedFolderType.Lora] = new[] { "models/Lora" },
            [SharedFolderType.LyCORIS] = new[] { "models/LyCORIS" },
            [SharedFolderType.ControlNet] = new[] { "models/ControlNet" }
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
                Options = ["--server-name"]
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = ["--port"]
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
                Options = ["--lowvram", "--medvram"]
            },
            new()
            {
                Name = "Auto-Launch Web UI",
                Type = LaunchOptionType.Bool,
                Options = ["--autolaunch"]
            },
            new()
            {
                Name = "Force use of Intel OneAPI XPU backend",
                Type = LaunchOptionType.Bool,
                Options = ["--use-ipex"]
            },
            new()
            {
                Name = "Use DirectML if no compatible GPU is detected",
                Type = LaunchOptionType.Bool,
                Options = ["--use-directml"]
            },
            new()
            {
                Name = "Force use of Nvidia CUDA backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.HasNvidiaGpu(),
                Options = ["--use-cuda"]
            },
            new()
            {
                Name = "Force use of Intel OneAPI XPU backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.HasIntelGpu(),
                Options = ["--use-ipex"]
            },
            new()
            {
                Name = "Force use of AMD ROCm backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferRocm(),
                Options = ["--use-rocm"]
            },
            new()
            {
                Name = "Force use of ZLUDA backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferDirectML(),
                Options = ["--use-zluda"]
            },
            new()
            {
                Name = "CUDA Device ID",
                Type = LaunchOptionType.String,
                Options = ["--device-id"]
            },
            new()
            {
                Name = "API",
                Type = LaunchOptionType.Bool,
                Options = ["--api"]
            },
            new()
            {
                Name = "Debug Logging",
                Type = LaunchOptionType.Bool,
                Options = ["--debug"]
            },
            LaunchOptionDefinition.Extras
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
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        var torchVersion = options.PythonOptions.TorchVersion ?? GetRecommendedTorchVersion();
        switch (torchVersion)
        {
            // Run initial install
            case TorchVersion.Cuda:
                await venvRunner
                    .CustomInstall("launch.py --use-cuda --debug --test", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchVersion.Rocm:
                await venvRunner
                    .CustomInstall("launch.py --use-rocm --debug --test", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchVersion.DirectMl:
                await venvRunner
                    .CustomInstall("launch.py --use-directml --debug --test", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchVersion.Zluda:
                await venvRunner
                    .CustomInstall("launch.py --use-zluda --debug --test", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchVersion.Ipex:
                await venvRunner
                    .CustomInstall("launch.py --use-ipex --debug --test", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            default:
                // CPU
                await venvRunner
                    .CustomInstall("launch.py --debug --test", onConsoleOutput)
                    .ConfigureAwait(false);
                break;
        }

        progress?.Report(new ProgressReport(1f, isIndeterminate: false));
    }

    public override async Task DownloadPackage(
        string installLocation,
        DownloadPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(
            new ProgressReport(
                -1f,
                message: "Downloading package...",
                isIndeterminate: true,
                type: ProgressType.Download
            )
        );

        var installDir = new DirectoryPath(installLocation);
        installDir.Create();

        var versionOptions = options.VersionOptions;

        if (string.IsNullOrWhiteSpace(versionOptions.BranchName))
        {
            throw new InvalidOperationException("Branch name is required for VladAutomatic");
        }

        await PrerequisiteHelper
            .RunGit(
                new[]
                {
                    "clone",
                    "-b",
                    versionOptions.BranchName,
                    "https://github.com/vladmandic/automatic",
                    installDir.Name
                },
                installDir.Parent?.FullPath ?? ""
            )
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(versionOptions.CommitHash) && !versionOptions.IsLatest)
        {
            await PrerequisiteHelper
                .RunGit(new[] { "checkout", versionOptions.CommitHash }, installLocation)
                .ConfigureAwait(false);
        }
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
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), ..options.Arguments],
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

        await using var venvRunner = await SetupVenvPure(installedPackage.FullPath!.Unwrap())
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
                IsPrerelease = false
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

    public override Task SetupModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    )
    {
        switch (sharedFolderMethod)
        {
            case SharedFolderMethod.Symlink:
                return base.SetupModelFolders(installDirectory, sharedFolderMethod);
            case SharedFolderMethod.None:
                return Task.CompletedTask;
        }

        // Config option
        var configJsonPath = installDirectory + "config.json";
        var exists = File.Exists(configJsonPath);
        JsonObject? configRoot;
        if (exists)
        {
            var configJson = File.ReadAllText(configJsonPath);
            try
            {
                configRoot = JsonSerializer.Deserialize<JsonObject>(configJson) ?? new JsonObject();
            }
            catch (JsonException e)
            {
                Logger.Error(e, "Error setting up Vlad shared model config");
                return Task.CompletedTask;
            }
        }
        else
        {
            configRoot = new JsonObject();
        }

        configRoot["ckpt_dir"] = Path.Combine(SettingsManager.ModelsDirectory, "StableDiffusion");
        configRoot["diffusers_dir"] = Path.Combine(SettingsManager.ModelsDirectory, "Diffusers");
        configRoot["vae_dir"] = Path.Combine(SettingsManager.ModelsDirectory, "VAE");
        configRoot["lora_dir"] = Path.Combine(SettingsManager.ModelsDirectory, "Lora");
        configRoot["lyco_dir"] = Path.Combine(SettingsManager.ModelsDirectory, "LyCORIS");
        configRoot["embeddings_dir"] = Path.Combine(SettingsManager.ModelsDirectory, "TextualInversion");
        configRoot["hypernetwork_dir"] = Path.Combine(SettingsManager.ModelsDirectory, "Hypernetwork");
        configRoot["codeformer_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "Codeformer");
        configRoot["gfpgan_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "GFPGAN");
        configRoot["bsrgan_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "BSRGAN");
        configRoot["esrgan_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "ESRGAN");
        configRoot["realesrgan_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "RealESRGAN");
        configRoot["scunet_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "ScuNET");
        configRoot["swinir_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "SwinIR");
        configRoot["ldsr_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "LDSR");
        configRoot["clip_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "CLIP");
        configRoot["control_net_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "ControlNet");

        var configJsonStr = JsonSerializer.Serialize(
            configRoot,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(configJsonPath, configJsonStr);

        return Task.CompletedTask;
    }

    public override Task UpdateModelFolders(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) =>
        sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink => base.UpdateModelFolders(installDirectory, sharedFolderMethod),
            SharedFolderMethod.None => Task.CompletedTask,
            SharedFolderMethod.Configuration => SetupModelFolders(installDirectory, sharedFolderMethod),
            _ => Task.CompletedTask
        };

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) =>
        sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink => base.RemoveModelFolderLinks(installDirectory, sharedFolderMethod),
            SharedFolderMethod.None => Task.CompletedTask,
            SharedFolderMethod.Configuration => RemoveConfigSettings(installDirectory),
            _ => Task.CompletedTask
        };

    private Task RemoveConfigSettings(string installDirectory)
    {
        var configJsonPath = Path.Combine(installDirectory, "config.json");
        var exists = File.Exists(configJsonPath);
        JsonObject? configRoot;
        if (exists)
        {
            var configJson = File.ReadAllText(configJsonPath);
            try
            {
                configRoot = JsonSerializer.Deserialize<JsonObject>(configJson);
                if (configRoot == null)
                {
                    return Task.CompletedTask;
                }
            }
            catch (JsonException e)
            {
                Logger.Error(e, "Error removing Vlad shared model config");
                return Task.CompletedTask;
            }
        }
        else
        {
            return Task.CompletedTask;
        }

        configRoot.Remove("ckpt_dir");
        configRoot.Remove("diffusers_dir");
        configRoot.Remove("vae_dir");
        configRoot.Remove("lora_dir");
        configRoot.Remove("lyco_dir");
        configRoot.Remove("embeddings_dir");
        configRoot.Remove("hypernetwork_dir");
        configRoot.Remove("codeformer_models_path");
        configRoot.Remove("gfpgan_models_path");
        configRoot.Remove("bsrgan_models_path");
        configRoot.Remove("esrgan_models_path");
        configRoot.Remove("realesrgan_models_path");
        configRoot.Remove("scunet_models_path");
        configRoot.Remove("swinir_models_path");
        configRoot.Remove("ldsr_models_path");
        configRoot.Remove("clip_models_path");
        configRoot.Remove("control_net_models_path");

        var configJsonStr = JsonSerializer.Serialize(
            configRoot,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(configJsonPath, configJsonStr);

        return Task.CompletedTask;
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

                return jsonManifest?.Select(
                        entry =>
                            new PackageExtension
                            {
                                Title = entry.Name,
                                Author = entry.Long?.Split('/').FirstOrDefault() ?? "Unknown",
                                Reference = entry.Url,
                                Files = [entry.Url],
                                Description = entry.Description,
                                InstallType = "git-clone"
                            }
                    ) ?? Enumerable.Empty<PackageExtension>();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get extensions from manifest");
                return Enumerable.Empty<PackageExtension>();
            }
        }
    }
}
