using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Helper;
using StabilityMatrix.Services;

namespace StabilityMatrix.Models.Packages;

public class ComfyUI : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public override string Name => "ComfyUI";
    public override string DisplayName { get; set; } = "ComfyUI";
    public override string Author => "comfyanonymous";
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
    
    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new()
        {
            Name = "VRAM",
            InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
            {
                Level.Low => "--lowvram",
                Level.Medium => "--normalvram",
                _ => null
            },
            Options = new() { "--highvram", "--normalvram", "--lowvram", "--novram" }
        },
        new()
        {
            Name = "Use CPU only",
            InitialValue = !HardwareHelper.HasNvidiaGpu(),
            Options = new() {"--cpu"}
        },
        new()
        {
            Name = "Disable Xformers",
            InitialValue = !HardwareHelper.HasNvidiaGpu(),
            Options = new() { "--disable-xformers" }
        },
        new()
        {
            Name = "Auto-Launch",
            Options = new() { "--auto-launch" }
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
        await PrerequisiteHelper.SetupPythonDependencies(InstallLocation, "requirements.txt", progress,
            OnConsoleOutput);
    }
    
    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath);
        PrerequisiteHelper.UpdatePathExtensions();

        void HandleConsoleOutput(string? s)
        {
            if (s == null) return;

            if (s.Contains("To see the GUI go to", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s);
                if (match.Success)
                {
                    WebUrl = match.Value;
                }
                OnStartupComplete(WebUrl);
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
