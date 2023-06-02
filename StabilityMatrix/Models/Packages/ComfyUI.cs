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

namespace StabilityMatrix.Models.Packages;

public class ComfyUI : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public override string Name => "ComfyUI";
    public override string DisplayName { get; set; } = "ComfyUI";
    public override string Author => "comfyanonymous";
    public override string LaunchCommand => "main.py";
    public override bool ShouldIgnoreReleases => true;

    public ComfyUI(IGithubApiCache githubApi, ISettingsManager settingsManager) : base(githubApi, settingsManager)
    {
    }

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new()
        {
            Name = "VRAM",
            Options = new() { "--lowvram", "--medvram" }
        },
        new()
        {
            Name = "Xformers",
            Options = new() { "--xformers" }
        },
        new()
        {
            Name = "API",
            Options = new() { "--api" }
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
        
        void OnConsoleOutput(string? s)
        {
            Debug.WriteLine($"venv stdout: {s}");
        }
        
        // Install torch
        Logger.Debug("Starting torch install...");
        await venvRunner.PipInstall(venvRunner.GetTorchInstallCommand(), InstallLocation, OnConsoleOutput);
        // Install requirements
        Logger.Debug("Starting requirements install...");
        await venvRunner.PipInstall("-r requirements.txt", InstallLocation, OnConsoleOutput);
        
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
