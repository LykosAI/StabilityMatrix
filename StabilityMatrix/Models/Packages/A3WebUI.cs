using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Models.Packages;

public class A3WebUI : BaseGitPackage
{
    public override string Name => "stable-diffusion-webui";
    public override string DisplayName { get; set; } = "stable-diffusion-webui";
    public override string Author => "AUTOMATIC1111";
    public override string LaunchCommand => "launch.py";
    public override string DefaultLaunchArguments => $"{GetVramOption()} {GetXformersOption()}";

    
    public A3WebUI(IGithubApi githubApi, ISettingsManager settingsManager) : base(githubApi, settingsManager) { }

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new()
        {
            Name = "API",
            DefaultValue = true,
            Options = new() { "--api" }
        },
        new()
        {
            Name = "Host",
            Type = "string",
            DefaultValue = "localhost",
            Options = new() { "--host" }
        },
        new()
        {
            Name = "Port",
            Type = "int",
            DefaultValue = 7860,
            Options = new() { "--port" }
        },
        new()
        {
            Name = "VRAM",
            Options = new() { "--lowvram", "--medvram" }
        },
        new()
        {
            Name = "Xformers",
            Options = new() { "--xformers" }
        }
    };

    public override async Task<string> GetLatestVersion()
    {
        var release = await GetLatestRelease();
        return release.TagName!;
    }

    public override async Task<IEnumerable<PackageVersion>> GetAllVersions(bool isReleaseMode = true)
    {
        if (isReleaseMode)
        {
            var allReleases = await GetAllReleases();
            return allReleases.Select(r => new PackageVersion {TagName = r.TagName!, ReleaseNotesMarkdown = r.Body});
        }
        else // branch mode1
        {
            var allBranches = await GetAllBranches();
            return allBranches.Select(b => new PackageVersion
            {
                TagName = $"{b.Name}",
                ReleaseNotesMarkdown = string.Empty
            });
        }
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
            OnConsoleOutput($"Venv process exited with code {i}");
        }

        var args = $"\"{Path.Combine(installedPackagePath, LaunchCommand)}\" {arguments}";

        VenvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, HandleExit, workingDirectory: installedPackagePath);
    }

    private static string GetVramOption()
    {
        var vramGb = HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024;

        return vramGb switch
        {
            < 4 => "--lowvram",
            < 8 => "--medvram",
            _ => string.Empty
        };
    }

    private static string GetXformersOption()
    {
        var gpuName = HardwareHelper.GetGpuChipName();
        return gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "--xformers" : string.Empty;
    }
}
