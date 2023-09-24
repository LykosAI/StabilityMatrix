using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class VladAutomatic : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public override string Name => "automatic";
    public override string DisplayName { get; set; } = "SD.Next Web UI";
    public override string Author => "vladmandic";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl =>
        "https://github.com/vladmandic/automatic/blob/master/LICENSE.txt";
    public override string Blurb => "Stable Diffusion implementation with advanced features";
    public override string LaunchCommand => "launch.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/vladmandic/automatic/raw/master/html/black-orange.jpg");
    public override bool ShouldIgnoreReleases => true;

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[] { TorchVersion.Cpu, TorchVersion.Rocm, TorchVersion.DirectMl, TorchVersion.Cuda };

    public VladAutomatic(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper
    )
        : base(githubApi, settingsManager, downloadService, prerequisiteHelper) { }

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

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public override List<LaunchOptionDefinition> LaunchOptions =>
        new()
        {
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = new() { "--server-name" }
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = new() { "--port" }
            },
            new()
            {
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper
                    .IterGpuInfo()
                    .Select(gpu => gpu.MemoryLevel)
                    .Max() switch
                {
                    Level.Low => "--lowvram",
                    Level.Medium => "--medvram",
                    _ => null
                },
                Options = new() { "--lowvram", "--medvram" }
            },
            new()
            {
                Name = "Auto-Launch Web UI",
                Type = LaunchOptionType.Bool,
                Options = new() { "--autolaunch" }
            },
            new()
            {
                Name = "Force use of Intel OneAPI XPU backend",
                Type = LaunchOptionType.Bool,
                Options = new() { "--use-ipex" }
            },
            new()
            {
                Name = "Use DirectML if no compatible GPU is detected",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferDirectML(),
                Options = new() { "--use-directml" }
            },
            new()
            {
                Name = "Force use of Nvidia CUDA backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.HasNvidiaGpu(),
                Options = new() { "--use-cuda" }
            },
            new()
            {
                Name = "Force use of AMD ROCm backend",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.PreferRocm(),
                Options = new() { "--use-rocm" }
            },
            new()
            {
                Name = "CUDA Device ID",
                Type = LaunchOptionType.String,
                Options = new() { "--device-id" }
            },
            new()
            {
                Name = "API",
                Type = LaunchOptionType.Bool,
                Options = new() { "--api" }
            },
            new()
            {
                Name = "Debug Logging",
                Type = LaunchOptionType.Bool,
                Options = new() { "--debug" }
            },
            LaunchOptionDefinition.Extras
        };

    public override string ExtraLaunchArguments => "";

    public override Task<string> GetLatestVersion() => Task.FromResult("master");

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(new ProgressReport(-1f, "Installing package...", isIndeterminate: true));
        // Setup venv
        var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));
        venvRunner.WorkingDirectory = installLocation;
        venvRunner.EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables;

        await venvRunner.Setup(true).ConfigureAwait(false);

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
        DownloadPackageVersionOptions downloadOptions,
        IProgress<ProgressReport>? progress = null
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

        if (!string.IsNullOrWhiteSpace(downloadOptions.CommitHash))
        {
            await PrerequisiteHelper
                .RunGit(
                    installDir.Parent ?? "",
                    "clone",
                    "https://github.com/vladmandic/automatic",
                    installDir.Name
                )
                .ConfigureAwait(false);

            await PrerequisiteHelper
                .RunGit(installLocation, "checkout", downloadOptions.CommitHash)
                .ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(downloadOptions.BranchName))
        {
            await PrerequisiteHelper
                .RunGit(
                    installDir.Parent ?? "",
                    "clone",
                    "-b",
                    downloadOptions.BranchName,
                    "https://github.com/vladmandic/automatic",
                    installDir.Name
                )
                .ConfigureAwait(false);
        }
    }

    public override async Task RunPackage(
        string installedPackagePath,
        string command,
        string arguments,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

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

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnExit(i);
        }

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, HandleExit);
    }

    public override async Task<InstalledPackageVersion> Update(
        InstalledPackage installedPackage,
        TorchVersion torchVersion,
        IProgress<ProgressReport>? progress = null,
        bool includePrerelease = false,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        if (installedPackage.Version is null)
        {
            throw new Exception("Version is null");
        }

        progress?.Report(
            new ProgressReport(
                -1f,
                message: "Downloading package update...",
                isIndeterminate: true,
                type: ProgressType.Update
            )
        );

        await PrerequisiteHelper
            .RunGit(installedPackage.FullPath, "checkout", installedPackage.Version.InstalledBranch)
            .ConfigureAwait(false);

        var venvRunner = new PyVenvRunner(Path.Combine(installedPackage.FullPath!, "venv"));
        venvRunner.WorkingDirectory = installedPackage.FullPath!;
        venvRunner.EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables;

        await venvRunner
            .CustomInstall("launch.py --upgrade --test", onConsoleOutput)
            .ConfigureAwait(false);

        try
        {
            var output = await PrerequisiteHelper
                .GetGitOutput(installedPackage.FullPath, "rev-parse", "HEAD")
                .ConfigureAwait(false);

            return new InstalledPackageVersion
            {
                InstalledBranch = installedPackage.Version.InstalledBranch,
                InstalledCommitSha = output.Replace(Environment.NewLine, "").Replace("\n", "")
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

        return new InstalledPackageVersion
        {
            InstalledBranch = installedPackage.Version.InstalledBranch
        };
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
        configRoot["embeddings_dir"] = Path.Combine(
            SettingsManager.ModelsDirectory,
            "TextualInversion"
        );
        configRoot["hypernetwork_dir"] = Path.Combine(
            SettingsManager.ModelsDirectory,
            "Hypernetwork"
        );
        configRoot["codeformer_models_path"] = Path.Combine(
            SettingsManager.ModelsDirectory,
            "Codeformer"
        );
        configRoot["gfpgan_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "GFPGAN");
        configRoot["bsrgan_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "BSRGAN");
        configRoot["esrgan_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "ESRGAN");
        configRoot["realesrgan_models_path"] = Path.Combine(
            SettingsManager.ModelsDirectory,
            "RealESRGAN"
        );
        configRoot["scunet_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "ScuNET");
        configRoot["swinir_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "SwinIR");
        configRoot["ldsr_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "LDSR");
        configRoot["clip_models_path"] = Path.Combine(SettingsManager.ModelsDirectory, "CLIP");
        configRoot["control_net_models_path"] = Path.Combine(
            SettingsManager.ModelsDirectory,
            "ControlNet"
        );

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
    ) => SetupModelFolders(installDirectory, sharedFolderMethod);

    public override Task RemoveModelFolderLinks(
        DirectoryPath installDirectory,
        SharedFolderMethod sharedFolderMethod
    ) =>
        sharedFolderMethod switch
        {
            SharedFolderMethod.Symlink
                => base.RemoveModelFolderLinks(installDirectory, sharedFolderMethod),
            SharedFolderMethod.None => Task.CompletedTask,
            _ => Task.CompletedTask
        };
}
