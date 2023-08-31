using System.Diagnostics.CodeAnalysis;
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

public class A3WebUI : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public override string Name => "stable-diffusion-webui";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI";
    public override string Author => "AUTOMATIC1111";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => 
        "https://github.com/AUTOMATIC1111/stable-diffusion-webui/blob/master/LICENSE.txt";
    public override string Blurb =>
        "A browser interface based on Gradio library for Stable Diffusion";
    public override string LaunchCommand => "launch.py";
    public override Uri PreviewImageUri =>
        new("https://github.com/AUTOMATIC1111/stable-diffusion-webui/raw/master/screenshot.png");
    public string RelativeArgsDefinitionScriptPath => "modules.cmd_args";

    public override SharedFolderMethod RecommendedSharedFolderMethod =>
        SharedFolderMethod.Symlink;

    public A3WebUI(IGithubApiCache githubApi, ISettingsManager settingsManager, IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) :
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }

    // From https://github.com/AUTOMATIC1111/stable-diffusion-webui/tree/master/models
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = new[] {"models/Stable-diffusion"},
        [SharedFolderType.ESRGAN] = new[] {"models/ESRGAN"},
        [SharedFolderType.RealESRGAN] = new[] {"models/RealESRGAN"},
        [SharedFolderType.SwinIR] = new[] {"models/SwinIR"},
        [SharedFolderType.Lora] = new[] {"models/Lora"},
        [SharedFolderType.LyCORIS] = new[] {"models/LyCORIS"},
        [SharedFolderType.ApproxVAE] = new[] {"models/VAE-approx"},
        [SharedFolderType.VAE] = new[] {"models/VAE"},
        [SharedFolderType.DeepDanbooru] = new[] {"models/deepbooru"},
        [SharedFolderType.Karlo] = new[] {"models/karlo"},
        [SharedFolderType.TextualInversion] = new[] {"embeddings"},
        [SharedFolderType.Hypernetwork] = new[] {"models/hypernetworks"},
        [SharedFolderType.ControlNet] = new[] {"models/ControlNet"}
    };

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new()
        {
            Name = "Host",
            Type = LaunchOptionType.String,
            DefaultValue = "localhost",
            Options = new() {"--host"}
        },
        new()
        {
            Name = "Port",
            Type = LaunchOptionType.String,
            DefaultValue = "7860",
            Options = new() {"--port"}
        },
        new()
        {
            Name = "VRAM",
            Type = LaunchOptionType.Bool,
            InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
            {
                Level.Low => "--lowvram",
                Level.Medium => "--medvram",
                _ => null
            },
            Options = new() { "--lowvram", "--medvram", "--medvram-sdxl" }
        },
        new()
        {
            Name = "Xformers",
            Type = LaunchOptionType.Bool,
            InitialValue = HardwareHelper.HasNvidiaGpu(),
            Options = new() { "--xformers" }
        },
        new()
        {
            Name = "API",
            Type = LaunchOptionType.Bool,
            InitialValue = true,
            Options = new() {"--api"}
        },
        new()
        {
            Name = "Skip Torch CUDA Check",
            Type = LaunchOptionType.Bool,
            InitialValue = !HardwareHelper.HasNvidiaGpu(),
            Options = new() {"--skip-torch-cuda-test"}
        },
        new()
        {
            Name = "Skip Python Version Check",
            Type = LaunchOptionType.Bool,
            InitialValue = true,
            Options = new() {"--skip-python-version-check"}
        },
        new()
        {
            Name = "No Half",
            Type = LaunchOptionType.Bool,
            Description = "Do not switch the model to 16-bit floats",
            InitialValue = HardwareHelper.HasAmdGpu(),
            Options = new() {"--no-half"}
        },
        LaunchOptionDefinition.Extras
    };

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods => new[]
    {
        SharedFolderMethod.Symlink,
        SharedFolderMethod.None
    };

    public override IEnumerable<TorchVersion> AvailableTorchVersions => new[]
    {
        TorchVersion.Cpu,
        TorchVersion.Cuda,
        TorchVersion.DirectMl,
        TorchVersion.Rocm
    };

    public override async Task<string> GetLatestVersion()
    {
        var release = await GetLatestRelease().ConfigureAwait(false);
        return release.TagName!;
    }

    public override async Task InstallPackage(string installLocation,
        TorchVersion torchVersion, IProgress<ProgressReport>? progress = null)
    {
        await base.InstallPackage(installLocation, torchVersion, progress).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));
        venvRunner.WorkingDirectory = installLocation;
        if (!venvRunner.Exists())
        {
            await venvRunner.Setup().ConfigureAwait(false);
        }

        switch (torchVersion)
        {
            case TorchVersion.Cpu:
                await InstallCpuTorch(venvRunner, progress).ConfigureAwait(false);
                break;
            case TorchVersion.Cuda:
                await InstallCudaTorch(venvRunner, progress).ConfigureAwait(false);
                break;
            case TorchVersion.Rocm:
                await InstallRocmTorch(venvRunner, progress).ConfigureAwait(false);
                break;
            case TorchVersion.DirectMl:
                await InstallDirectMlTorch(venvRunner, progress).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null);
        }

        // Install requirements file
        progress?.Report(new ProgressReport(-1f, "Installing Package Requirements",
            isIndeterminate: true));
        Logger.Info("Installing requirements_versions.txt");
        await venvRunner.PipInstall($"-r requirements_versions.txt", OnConsoleOutput)
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "Installing Package Requirements",
            isIndeterminate: false));

        progress?.Report(new ProgressReport(-1f, "Updating configuration", isIndeterminate: true));

        // Create and add {"show_progress_type": "TAESD"} to config.json
        // Only add if the file doesn't exist
        var configPath = Path.Combine(installLocation, "config.json");
        if (!File.Exists(configPath))
        {
            var config = new JsonObject {{"show_progress_type", "TAESD"}};
            await File.WriteAllTextAsync(configPath, config.ToString()).ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }

    public override async Task RunPackage(string installedPackagePath, string command, string arguments)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        void HandleConsoleOutput(ProcessOutput s)
        {
            OnConsoleOutput(s);

            if (!s.Text.Contains("Running on", StringComparison.OrdinalIgnoreCase)) 
                return;
            
            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (!match.Success)
                return;
            
            WebUrl = match.Value;
            OnStartupComplete(WebUrl);
        }

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, OnExit);
    }

    private async Task InstallRocmTorch(PyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(new ProgressReport(-1f, "Installing PyTorch for ROCm",
            isIndeterminate: true));

        await venvRunner.PipInstall("--upgrade pip wheel", OnConsoleOutput)
            .ConfigureAwait(false);

        await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsRocm511, OnConsoleOutput)
            .ConfigureAwait(false);
    }
}
