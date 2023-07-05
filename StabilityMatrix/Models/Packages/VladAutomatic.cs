using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Models.Progress;
using StabilityMatrix.Python;
using StabilityMatrix.Services;

namespace StabilityMatrix.Models.Packages;

public class VladAutomatic : BaseGitPackage
{
    public override string Name => "automatic";
    public override string DisplayName { get; set; } = "SD.Next Web UI";
    public override string Author => "vladmandic";
    public override string LaunchCommand => "launch.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/vladmandic/automatic/raw/master/html/black-orange.jpg");
    public override bool ShouldIgnoreReleases => true;

    public VladAutomatic(IGithubApiCache githubApi, ISettingsManager settingsManager, IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) :
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }

    // https://github.com/vladmandic/automatic/blob/master/modules/shared.py#L324
    public override Dictionary<SharedFolderType, string> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = "models/Stable-diffusion",
        [SharedFolderType.Diffusers] = "models/Diffusers",
        [SharedFolderType.VAE] = "models/VAE",
        [SharedFolderType.TextualInversion] = "models/embeddings",
        [SharedFolderType.Hypernetwork] = "models/hypernetworks",
        [SharedFolderType.Codeformer] = "models/Codeformer",
        [SharedFolderType.GFPGAN] = "models/GFPGAN",
        [SharedFolderType.BSRGAN] = "models/BSRGAN",
        [SharedFolderType.ESRGAN] = "models/ESRGAN",
        [SharedFolderType.RealESRGAN] = "models/RealESRGAN",
        [SharedFolderType.ScuNET] = "models/ScuNET",
        [SharedFolderType.SwinIR] = "models/SwinIR",
        [SharedFolderType.LDSR] = "models/LDSR",
        [SharedFolderType.CLIP] = "models/CLIP",
        [SharedFolderType.Lora] = "models/Lora",
        [SharedFolderType.LyCORIS] = "models/LyCORIS",
    };

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new()
        {
            Name = "Host",
            Type = LaunchOptionType.String,
            DefaultValue = "localhost",
            Options = new() {"--server-name"}
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
            Name = "Force use of Intel OneAPI XPU backend",
            Options = new() { "--use-ipex" }
        },
        new()
        {
            Name = "Use DirectML if no compatible GPU is detected",
            InitialValue = !HardwareHelper.HasNvidiaGpu() && HardwareHelper.HasAmdGpu(),
            Options = new() { "--use-directml" }
        },
        new()
        {
            Name = "Force use of Nvidia CUDA backend",
            Options = new() { "--use-cuda" }
        },
        new()
        {
            Name = "Force use of AMD ROCm backend",
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
            Options = new() { "--api" }
        },
        new()
        {
            Name = "Debug Logging",
            Options = new() { "--debug" }
        },
        LaunchOptionDefinition.Extras
    };

    public override string ExtraLaunchArguments => "";

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
        await PrerequisiteHelper.SetupPythonDependencies(InstallLocation, "requirements.txt", progress,
            OnConsoleOutput);
    }

    public override async Task<string?> DownloadPackage(string version, bool isCommitHash, IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(new ProgressReport(0.1f, message: "Downloading package...", isIndeterminate: true, type: ProgressType.Download));
        
        Directory.CreateDirectory(InstallLocation);

        await PrerequisiteHelper.RunGit(null, "clone", "https://github.com/vladmandic/automatic",
            InstallLocation);
        await PrerequisiteHelper.RunGit(workingDirectory: InstallLocation, "checkout", version);
        
        return version;
    }

    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath);
        PrerequisiteHelper.UpdatePathExtensions();

        void HandleConsoleOutput(string? s)
        {
            if (s == null) return;
            if (s.Contains("Running on local URL", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s);
                if (match.Success)
                {
                    WebUrl = match.Value;
                    OnStartupComplete(WebUrl);
                }
            }
            Debug.WriteLine($"process stdout: {s}");
            OnConsoleOutput($"{s}\n");
        }

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnExit(i);
        }

        var args = $"\"{Path.Combine(installedPackagePath, LaunchCommand)}\" {arguments}";

        VenvRunner?.RunDetached(args.TrimEnd(), HandleConsoleOutput, HandleExit, workingDirectory: installedPackagePath);
    }
}
