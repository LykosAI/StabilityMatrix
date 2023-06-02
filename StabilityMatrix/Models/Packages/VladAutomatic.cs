using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Helper.Cache;

namespace StabilityMatrix.Models.Packages;

public class VladAutomatic : BaseGitPackage
{
    public override string Name => "automatic";
    public override string DisplayName { get; set; } = "SD.Next Web UI";
    public override string Author => "vladmandic";
    public override string LaunchCommand => "launch.py";
    public override bool ShouldIgnoreReleases => true;

    public VladAutomatic(IGithubApiCache githubApi, ISettingsManager settingsManager) : base(githubApi, settingsManager)
    {
    }

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
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
            Name = "Xformers",
            InitialValue = HardwareHelper.HasNvidiaGpu() ? "--xformers" : null,
            Options = new() { "--xformers" }
        },
        new()
        {
            Name = "API",
            Options = new() { "--api" }
        },
        LaunchOptionDefinition.Extras
    };

    public override string ExtraLaunchArguments => "--skip-git";

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

    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath);

        void HandleConsoleOutput(string? s)
        {
            if (s == null) return;
            if (s.Contains("model loaded", StringComparison.OrdinalIgnoreCase))
            {
                OnStartupComplete(WebUrl);
            }
            if (s.Contains("Running on", StringComparison.OrdinalIgnoreCase))
            {
                WebUrl = s.Split(" ")[5];
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
