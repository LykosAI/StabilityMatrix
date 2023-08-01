using System.Diagnostics;
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
            Options = new() { "--lowvram", "--medvram" }
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
        LaunchOptionDefinition.Extras
    };
    
    public override async Task<string> GetLatestVersion()
    {
        var release = await GetLatestRelease().ConfigureAwait(false);
        return release.TagName!;
    }

    public override async Task InstallPackage(IProgress<ProgressReport>? progress = null)
    {
        await UnzipPackage(progress);
        
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = new PyVenvRunner(Path.Combine(InstallLocation, "venv"));
        venvRunner.WorkingDirectory = InstallLocation;
        if (!venvRunner.Exists())
        {
            await venvRunner.Setup().ConfigureAwait(false);
        }

        // Install torch / xformers based on gpu info
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        if (gpus.Any(g => g.IsNvidia))
        {
            progress?.Report(new ProgressReport(-1f, "Installing PyTorch for CUDA", isIndeterminate: true));
            
            Logger.Info("Starting torch install (CUDA)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCuda, OnConsoleOutput)
                .ConfigureAwait(false);
            
            Logger.Info("Installing xformers...");
            await venvRunner.PipInstall("xformers", OnConsoleOutput).ConfigureAwait(false);
        }
        else
        {
            progress?.Report(new ProgressReport(-1f, "Installing PyTorch for CPU", isIndeterminate: true));
            Logger.Info("Starting torch install (CPU)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCpu, OnConsoleOutput).ConfigureAwait(false);
        }

        // Install requirements file
        progress?.Report(new ProgressReport(-1f, "Installing Package Requirements", isIndeterminate: true));
        Logger.Info("Installing requirements_versions.txt");
        await venvRunner.PipInstall($"-r requirements_versions.txt", OnConsoleOutput).ConfigureAwait(false);
        
        progress?.Report(new ProgressReport(1f, "Installing Package Requirements", isIndeterminate: false));
        
        progress?.Report(new ProgressReport(-1f, "Updating configuration", isIndeterminate: true));
        // Create and add {"show_progress_type": "TAESD"} to config.json
        var configPath = Path.Combine(InstallLocation, "config.json");
        var config = new JsonObject {{"show_progress_type", "TAESD"}};
        await File.WriteAllTextAsync(configPath, config.ToString()).ConfigureAwait(false);

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

    public override Task SetupModelFolders(DirectoryPath installDirectory)
    {
        StabilityMatrix.Core.Helper.SharedFolders
            .SetupLinks(SharedFolders, SettingsManager.ModelsDirectory, installDirectory);
        return Task.CompletedTask;
    }

    public override async Task UpdateModelFolders(DirectoryPath installDirectory)
    {
        await StabilityMatrix.Core.Helper.SharedFolders.UpdateLinksForPackage(this,
            SettingsManager.ModelsDirectory, installDirectory).ConfigureAwait(false);
    }
}
