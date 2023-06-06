using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;
using StabilityMatrix.Python;
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

    public ComfyUI(IGithubApiCache githubApi, ISettingsManager settingsManager, IDownloadService downloadService) :
        base(githubApi, settingsManager, downloadService)
    {
    }

    // https://github.com/comfyanonymous/ComfyUI/blob/master/folder_paths.py#L11
    public override Dictionary<SharedFolderType, string> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = "models/Stable-diffusion",
        [SharedFolderType.Lora] = "models/loras",
        [SharedFolderType.CLIP] = "models/clip",
        [SharedFolderType.TextualInversion] = "models/embeddings",
        [SharedFolderType.Diffusers] = "models/diffusers",
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
            Options = new() { "--highvram", "--normalvram", "--lowvram", "--novram", "--cpu" }
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

    public override async Task InstallPackage(bool isUpdate = false)
    {
        UnzipPackage(isUpdate);
        
        // Setup dependencies
        OnInstallProgressChanged(-1); // Indeterminate progress bar
        // Setup venv
        Logger.Debug("Setting up venv");
        await SetupVenv(InstallLocation);
        var venvRunner = new PyVenvRunner(Path.Combine(InstallLocation, "venv"));
        
        void HandleConsoleOutput(string? s)
        {
            Debug.WriteLine($"venv stdout: {s}");
            OnConsoleOutput(s);
        }
        
        // Install torch
        Logger.Debug("Starting torch install...");
        await venvRunner.PipInstall(venvRunner.GetTorchInstallCommand(), InstallLocation, HandleConsoleOutput);
        
        // Install xformers if nvidia
        if (HardwareHelper.HasNvidiaGpu())
        {
            await venvRunner.PipInstall("xformers", InstallLocation, HandleConsoleOutput);
        }

        // Install requirements
        Logger.Debug("Starting requirements install...");
        await venvRunner.PipInstall("-r requirements.txt", InstallLocation, HandleConsoleOutput);
        
        Logger.Debug("Finished installing requirements!");
        if (isUpdate)
        {
            OnUpdateComplete("Update complete");
        }
        else
        {
            OnInstallComplete("Install complete");
        }
    }
    
    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath);

        void HandleConsoleOutput(string? s)
        {
            if (s == null) return;

            if (s.Contains("To see the GUI go to", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(
                    @"(https?:\/\/)([^:\s]+):(\d+)");
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
