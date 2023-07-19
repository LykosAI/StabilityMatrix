﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
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
        IPrerequisiteHelper prerequisiteHelper, VladAutomaticSharedFolderStrategy sharedFolderStrategy) :
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
        SharedFolderStrategy = sharedFolderStrategy;
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

    public override ISharedFolderStrategy SharedFolderStrategy { get; protected set; }

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
        progress?.Report(new ProgressReport(-1, isIndeterminate: true));
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
            Logger.Info("Starting torch install (CUDA)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCuda, 
                InstallLocation, OnConsoleOutput);
            Logger.Info("Installing xformers...");
            await venvRunner.PipInstall("xformers", InstallLocation, OnConsoleOutput);
        }
        else if (gpus.Any(g => g.IsAmd))
        {
            Logger.Info("Starting torch install (DirectML)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsDirectML);
        }
        else
        {
            Logger.Info("Starting torch install (CPU)...");
            await venvRunner.PipInstall(PyVenvRunner.TorchPipInstallArgsCpu);
        }

        // Install requirements file
        Logger.Info("Installing requirements.txt");
        await venvRunner.PipInstall($"-r requirements.txt", InstallLocation, OnConsoleOutput);
        
        progress?.Report(new ProgressReport(1, isIndeterminate: false));
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

    public override async Task<string> Update(InstalledPackage installedPackage, IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(new ProgressReport(0.1f, message: "Downloading package update...", isIndeterminate: true, type: ProgressType.Download));

        var version = await GithubApi.GetAllCommits(Author, Name, installedPackage.InstalledBranch);
        var latest = version is {Count: > 0} ? version[0] : null;

        if (latest == null)
        {
            Logger.Warn("No latest version found for vlad");
            return string.Empty;
        }
        
        await PrerequisiteHelper.RunGit(workingDirectory: installedPackage.FullPath, "pull", "origin", installedPackage.InstalledBranch);
        
        progress?.Report(new ProgressReport(1f, message: "Update Complete", isIndeterminate: true, type: ProgressType.Generic));
        
        return latest.Sha;
    }
}
