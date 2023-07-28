using System.Diagnostics;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class ComfyUI : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public override string Name => "ComfyUI";
    public override string DisplayName { get; set; } = "ComfyUI";
    public override string Author => "comfyanonymous";
    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl => 
        "https://github.com/comfyanonymous/ComfyUI/blob/master/LICENSE";
    public override string Blurb => "A powerful and modular stable diffusion GUI and backend";
    public override string LaunchCommand => "main.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/comfyanonymous/ComfyUI/raw/master/comfyui_screenshot.png");
    public override bool ShouldIgnoreReleases => true;

    public ComfyUI(IGithubApiCache githubApi, ISettingsManager settingsManager, IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) :
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }

    // https://github.com/comfyanonymous/ComfyUI/blob/master/folder_paths.py#L11
    public override Dictionary<SharedFolderType, string> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = "models/checkpoints",
        [SharedFolderType.Diffusers] = "models/diffusers",
        [SharedFolderType.Lora] = "models/loras",
        [SharedFolderType.CLIP] = "models/clip",
        [SharedFolderType.TextualInversion] = "models/embeddings",
        [SharedFolderType.VAE] = "models/vae",
        [SharedFolderType.ApproxVAE] = "models/vae_approx",
        [SharedFolderType.ControlNet] = "models/controlnet",
        [SharedFolderType.GLIGEN] = "models/gligen",
        [SharedFolderType.ESRGAN] = "models/upscale_models",
        [SharedFolderType.Hypernetwork] = "models/hypernetworks",
    };
    
    public override List<LaunchOptionDefinition> LaunchOptions => new List<LaunchOptionDefinition>
    {
        new()
        {
            Name = "VRAM",
            Type = LaunchOptionType.Bool,
            InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
            {
                Level.Low => "--lowvram",
                Level.Medium => "--normalvram",
                _ => null
            },
            Options = { "--highvram", "--normalvram", "--lowvram", "--novram" }
        },
        new()
        {
            Name = "Use CPU only",
            Type = LaunchOptionType.Bool,
            InitialValue = !HardwareHelper.HasNvidiaGpu(),
            Options = {"--cpu"}
        },
        new()
        {
            Name = "Disable Xformers",
            Type = LaunchOptionType.Bool,
            InitialValue = !HardwareHelper.HasNvidiaGpu(),
            Options = { "--disable-xformers" }
        },
        new()
        {
            Name = "Auto-Launch",
            Type = LaunchOptionType.Bool,
            Options = { "--auto-launch" }
        },
        LaunchOptionDefinition.Extras
    };

    public override Task<string> GetLatestVersion() => Task.FromResult("master");

    public override async Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true)
    {
        var allBranches = await GetAllBranches();
        return allBranches.Select(b => new PackageVersion
        {
            TagName = $"{b.Name}", 
            ReleaseNotesMarkdown = string.Empty
        });
    }

    public override async Task InstallPackage(IProgress<ProgressReport>? progress = null)
    {
        await UnzipPackage(progress);
        
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        // Setup venv
        var venvRunner = new PyVenvRunner(Path.Combine(InstallLocation, "venv"));
        if (!venvRunner.Exists())
        {
            await venvRunner.Setup();
        }

        // Install torch / xformers based on gpu info
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        if (gpus.Any(g => g.IsNvidia))
        {
            progress?.Report(new ProgressReport(-1, "Installing PyTorch for CUDA", isIndeterminate: true));
            Logger.Info("Starting torch install (CUDA)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCuda, 
                InstallLocation, OnConsoleOutput);
            Logger.Info("Installing xformers...");
            await venvRunner.PipInstall("xformers", InstallLocation, OnConsoleOutput);
        }
        else
        {
            progress?.Report(new ProgressReport(-1, "Installing PyTorch for CPU", isIndeterminate: true));
            Logger.Info("Starting torch install (CPU)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCpu, InstallLocation, OnConsoleOutput);
        }

        // Install requirements file
        progress?.Report(new ProgressReport(-1, "Installing Package Requirements", isIndeterminate: true));
        Logger.Info("Installing requirements.txt");
        await venvRunner.PipInstall($"-r requirements.txt", InstallLocation, OnConsoleOutput);
        
        progress?.Report(new ProgressReport(1, "Installing Package Requirements", isIndeterminate: false));
    }
    
    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath);

        void HandleConsoleOutput(ProcessOutput s)
        {
            OnConsoleOutput(s);
            
            if (s.Text.Contains("To see the GUI go to", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (match.Success)
                {
                    WebUrl = match.Value;
                }
                OnStartupComplete(WebUrl);
            }
        }

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnExit(i);
        }

        var args = $"\"{Path.Combine(installedPackagePath, LaunchCommand)}\" {arguments}";

        VenvRunner?.RunDetached(
            args.TrimEnd(), 
            HandleConsoleOutput, 
            HandleExit, 
            workingDirectory: installedPackagePath,
            environmentVariables: SettingsManager.Settings.EnvironmentVariables);
    }
}
