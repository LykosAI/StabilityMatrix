using System.Diagnostics;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = new[] {"models/checkpoints"},
        [SharedFolderType.Diffusers] = new[] {"models/diffusers"},
        [SharedFolderType.Lora] = new[] {"models/loras"},
        [SharedFolderType.CLIP] = new[] {"models/clip"},
        [SharedFolderType.TextualInversion] = new[] {"models/embeddings"},
        [SharedFolderType.VAE] = new[] {"models/vae"},
        [SharedFolderType.ApproxVAE] = new[] {"models/vae_approx"},
        [SharedFolderType.ControlNet] = new[] {"models/controlnet"},
        [SharedFolderType.GLIGEN] = new[] {"models/gligen"},
        [SharedFolderType.ESRGAN] = new[] {"models/upscale_models"},
        [SharedFolderType.Hypernetwork] = new[] {"models/hypernetworks"},
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
        var allBranches = await GetAllBranches().ConfigureAwait(false);
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
            progress?.Report(new ProgressReport(-1, "Installing PyTorch for CUDA", isIndeterminate: true));
            
            Logger.Info("Starting torch install (CUDA)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCuda, OnConsoleOutput)
                .ConfigureAwait(false);
            
            Logger.Info("Installing xformers...");
            await venvRunner.PipInstall("xformers", OnConsoleOutput).ConfigureAwait(false);
        }
        else if (HardwareHelper.PreferRocm())
        {
            progress?.Report(new ProgressReport(-1, "Installing PyTorch for ROCm", isIndeterminate: true));
            await venvRunner
                .PipInstall(PyVenvRunner.TorchPipInstallArgsRocm542, OnConsoleOutput)
                .ConfigureAwait(false);
        }
        else
        {
            progress?.Report(new ProgressReport(-1, "Installing PyTorch for CPU", isIndeterminate: true));
            Logger.Info("Starting torch install (CPU)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCpu, OnConsoleOutput)
                .ConfigureAwait(false);
        }

        // Install requirements file
        progress?.Report(new ProgressReport(-1, "Installing Package Requirements", isIndeterminate: true));
        Logger.Info("Installing requirements.txt");
        await venvRunner.PipInstall($"-r requirements.txt", OnConsoleOutput).ConfigureAwait(false);
        
        progress?.Report(new ProgressReport(1, "Installing Package Requirements", isIndeterminate: false));
    }
    
    public override async Task RunPackage(string installedPackagePath, string command, string arguments)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

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

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner?.RunDetached(
            args.TrimEnd(), 
            HandleConsoleOutput, 
            HandleExit);
    }

    public override Task SetupModelFolders(DirectoryPath installDirectory)
    {
        var extraPathsYamlPath = installDirectory + "extra_model_paths.yaml";
        var modelsDir = SettingsManager.ModelsDirectory;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var exists = File.Exists(extraPathsYamlPath);
        if (!exists)
        {
            Logger.Info("Creating extra_model_paths.yaml");
            File.WriteAllText(extraPathsYamlPath, string.Empty);
        }
        var yaml = File.ReadAllText(extraPathsYamlPath);
        var comfyModelPaths = deserializer.Deserialize<ComfyModelPathsYaml>(yaml) ??
                              // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                              // cuz it can actually be null lol 
                              new ComfyModelPathsYaml();
        
        comfyModelPaths.StabilityMatrix ??= new ComfyModelPathsYaml.SmData();
        comfyModelPaths.StabilityMatrix.Checkpoints = Path.Combine(modelsDir, "StableDiffusion");
        comfyModelPaths.StabilityMatrix.Vae = Path.Combine(modelsDir, "VAE");
        comfyModelPaths.StabilityMatrix.Loras = $"{Path.Combine(modelsDir, "Lora")}\n" +
                                       $"{Path.Combine(modelsDir, "LyCORIS")}";
        comfyModelPaths.StabilityMatrix.UpscaleModels = $"{Path.Combine(modelsDir, "ESRGAN")}\n" +
                                              $"{Path.Combine(modelsDir, "RealESRGAN")}\n" +
                                              $"{Path.Combine(modelsDir, "SwinIR")}";
        comfyModelPaths.StabilityMatrix.Embeddings = Path.Combine(modelsDir, "TextualInversion");
        comfyModelPaths.StabilityMatrix.Hypernetworks = Path.Combine(modelsDir, "Hypernetwork");
        comfyModelPaths.StabilityMatrix.Controlnet = Path.Combine(modelsDir, "ControlNet");
        comfyModelPaths.StabilityMatrix.Clip = Path.Combine(modelsDir, "CLIP");
        comfyModelPaths.StabilityMatrix.Diffusers = Path.Combine(modelsDir, "Diffusers");
        comfyModelPaths.StabilityMatrix.Gligen = Path.Combine(modelsDir, "GLIGEN");
        comfyModelPaths.StabilityMatrix.VaeApprox = Path.Combine(modelsDir, "ApproxVAE");
        
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yamlData = serializer.Serialize(comfyModelPaths);
        File.WriteAllText(extraPathsYamlPath, yamlData);

        return Task.CompletedTask;
    }

    public override Task UpdateModelFolders(DirectoryPath installDirectory) =>
        SetupModelFolders(installDirectory);

    public override Task RemoveModelFolderLinks(DirectoryPath installDirectory) =>
        Task.CompletedTask;

    public class ComfyModelPathsYaml
    {
        public class SmData
        {
            public string Checkpoints { get; set; }
            public string Vae { get; set; }
            public string Loras { get; set; }
            public string UpscaleModels { get; set; }
            public string Embeddings { get; set; }
            public string Hypernetworks { get; set; }
            public string Controlnet { get; set; }
            public string Clip { get; set; }
            public string Diffusers { get; set; }
            public string Gligen { get; set; }
            public string VaeApprox { get; set; }
        }

        public SmData? StabilityMatrix { get; set; }
    }
}
