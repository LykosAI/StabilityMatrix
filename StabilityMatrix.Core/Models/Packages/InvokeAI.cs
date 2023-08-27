using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class InvokeAI : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string RelativeRootPath = "invokeai-root";

    public override string Name => "InvokeAI";
    public override string DisplayName { get; set; } = "InvokeAI";
    public override string Author => "invoke-ai";
    public override string LicenseType => "Apache-2.0";

    public override string LicenseUrl =>
        "https://github.com/invoke-ai/InvokeAI/blob/main/LICENSE";

    public override string Blurb => "Professional Creative Tools for Stable Diffusion";
    public override string LaunchCommand => "invokeai-web";

    public override IReadOnlyList<string> ExtraLaunchCommands => new[]
    {
        "invokeai-configure",
        "invokeai-merge",
        "invokeai-metadata",
        "invokeai-model-install",
        "invokeai-node-cli",
        "invokeai-ti",
        "invokeai-update",
    };

    public override Uri PreviewImageUri => new(
        "https://raw.githubusercontent.com/invoke-ai/InvokeAI/main/docs/assets/canvas_preview.png");

    public override bool ShouldIgnoreReleases => true;

    public InvokeAI(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) :
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }
    
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = new[] { RelativeRootPath + "/autoimport/main" },
        [SharedFolderType.Lora] = new[] { RelativeRootPath + "/autoimport/lora" },
        [SharedFolderType.TextualInversion] = new[] { RelativeRootPath + "/autoimport/embedding" },
        [SharedFolderType.ControlNet] = new[] { RelativeRootPath + "/autoimport/controlnet" },
    };

    // https://github.com/invoke-ai/InvokeAI/blob/main/docs/features/CONFIGURATION.md
    public override List<LaunchOptionDefinition> LaunchOptions => new List<LaunchOptionDefinition>
    {
        new()
        {
            Name = "Host",
            Type = LaunchOptionType.String,
            DefaultValue = "localhost",
            Options = new List<string> {"--host"}
        },
        new()
        {
            Name = "Port",
            Type = LaunchOptionType.String,
            DefaultValue = "9090",
            Options = new List<string> {"--port"}
        },
        new()
        {
            Name = "Allow Origins",
            Description = "List of host names or IP addresses that are allowed to connect to the " +
                          "InvokeAI API in the format ['host1','host2',...]",
            Type = LaunchOptionType.String,
            DefaultValue = "[]",
            Options = new List<string> {"--allow-origins"}
        },
        new()
        {
            Name = "Always use CPU",
            Type = LaunchOptionType.Bool,
            Options = new List<string> {"--always_use_cpu"}
        },
        new()
        {
            Name = "Precision",
            Type = LaunchOptionType.Bool,
            Options = new List<string>
            {
                "--precision auto",
                "--precision float16",
                "--precision float32",
            }
        },
        new()
        {
            Name = "Aggressively free up GPU memory after each operation",
            Type = LaunchOptionType.Bool,
            Options = new List<string> {"--free_gpu_mem"}
        },
        LaunchOptionDefinition.Extras
    };

    public override Task<string> GetLatestVersion() => Task.FromResult("main");

    public override Task DownloadPackage(string installLocation,
        DownloadPackageVersionOptions downloadOptions, IProgress<ProgressReport>? progress = null)
    {
        return Task.CompletedTask;
    }

    public override async Task InstallPackage(string installLocation, IProgress<ProgressReport>? progress = null)
    {
        // Setup venv
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));

        await using var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));
        venvRunner.WorkingDirectory = installLocation;
        await venvRunner.Setup().ConfigureAwait(false);

        venvRunner.EnvironmentVariables = GetEnvVars(installLocation);

        var gpus = HardwareHelper.IterGpuInfo().ToList();

        progress?.Report(new ProgressReport(-1f, "Installing Package", isIndeterminate: true));

        var pipCommandArgs =
            "InvokeAI --use-pep517 --extra-index-url https://download.pytorch.org/whl/cpu";

        // If has Nvidia Gpu, install CUDA version
        if (gpus.Any(g => g.IsNvidia))
        {
            Logger.Info("Starting InvokeAI install (CUDA)...");
            pipCommandArgs =
                "InvokeAI[xformers] --use-pep517 --extra-index-url https://download.pytorch.org/whl/cu117";
        }
        // For AMD, Install ROCm version
        else if (HardwareHelper.PreferRocm())
        {
            Logger.Info("Starting InvokeAI install (ROCm)...");
            pipCommandArgs =
                "InvokeAI --use-pep517 --extra-index-url https://download.pytorch.org/whl/rocm5.4.2";
        }
        // For Apple silicon, use MPS
        else if (Compat.IsMacOS && Compat.IsArm)
        {
            Logger.Info("Starting InvokeAI install (MPS)...");
            pipCommandArgs = "InvokeAI --use-pep517";
        }
        // CPU Version
        else
        {
            Logger.Info("Starting InvokeAI install (CPU)...");
        }

        await venvRunner.PipInstall(pipCommandArgs, OnConsoleOutput).ConfigureAwait(false);

        await venvRunner
            .PipInstall("rich packaging python-dotenv", OnConsoleOutput)
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Configuring InvokeAI", isIndeterminate: true));

        await RunInvokeCommand(installLocation, "invokeai-configure", "--yes --skip-sd-weights",
            false).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "Done!", isIndeterminate: false));
    }

    public override async Task<InstalledPackageVersion> Update(InstalledPackage installedPackage, IProgress<ProgressReport>? progress = null,
        bool includePrerelease = false)
    {
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));

        if (installedPackage.FullPath is null || installedPackage.Version is null)
        {
            throw new NullReferenceException("Installed package is missing Path and/or Version");
        }
        
        await using var venvRunner = new PyVenvRunner(Path.Combine(installedPackage.FullPath, "venv"));
        venvRunner.WorkingDirectory = installedPackage.FullPath;
        venvRunner.EnvironmentVariables = GetEnvVars(installedPackage.FullPath);

        var latestVersion = await GetUpdateVersion(installedPackage).ConfigureAwait(false);
        var isReleaseMode = installedPackage.Version.IsReleaseMode;

        var downloadUrl = isReleaseMode
            ? $"https://github.com/invoke-ai/InvokeAI/archive/{latestVersion}.zip"
            : $"https://github.com/invoke-ai/InvokeAI/archive/refs/heads/{installedPackage.Version.InstalledBranch}.zip";

        var gpus = HardwareHelper.IterGpuInfo().ToList();

        progress?.Report(new ProgressReport(-1f, "Installing Package", isIndeterminate: true));

        var pipCommandArgs =
            $"\"InvokeAI @ {downloadUrl}\" --use-pep517 --extra-index-url https://download.pytorch.org/whl/cpu --upgrade";

        // If has Nvidia Gpu, install CUDA version
        if (gpus.Any(g => g.IsNvidia))
        {
            Logger.Info("Starting InvokeAI install (CUDA)...");
            pipCommandArgs =
                $"\"InvokeAI[xformers] @ {downloadUrl}\" --use-pep517 --extra-index-url https://download.pytorch.org/whl/cu117 --upgrade";
        }
        // For AMD, Install ROCm version
        else if (gpus.Any(g => g.IsAmd))
        {
            Logger.Info("Starting InvokeAI install (ROCm)...");
            pipCommandArgs =
                $"\"InvokeAI @ {downloadUrl}\" --use-pep517 --extra-index-url https://download.pytorch.org/whl/rocm5.4.2 --upgrade";
        }
        // For Apple silicon, use MPS
        else if (Compat.IsMacOS && Compat.IsArm)
        {
            Logger.Info("Starting InvokeAI install (MPS)...");
            pipCommandArgs = $"\"InvokeAI @ {downloadUrl}\" --use-pep517 --upgrade";
        }
        // CPU Version
        else
        {
            Logger.Info("Starting InvokeAI install (CPU)...");
        }

        await venvRunner.PipInstall(pipCommandArgs, OnConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "Done!", isIndeterminate: false));

        return isReleaseMode
            ? new InstalledPackageVersion
            {
                InstalledReleaseVersion = latestVersion
            }
            : new InstalledPackageVersion
            {
                InstalledBranch = installedPackage.Version.InstalledBranch,
                InstalledCommitSha = latestVersion
            };
    }

    public override Task
        RunPackage(string installedPackagePath, string command, string arguments) =>
        RunInvokeCommand(installedPackagePath, command, arguments, true);

    private async Task<string> GetUpdateVersion(InstalledPackage installedPackage, bool includePrerelease = false)
    {
        if (installedPackage.Version == null)
            throw new NullReferenceException("Installed package version is null");
        
        if (installedPackage.Version.IsReleaseMode)
        {
            var releases = await GetAllReleases().ConfigureAwait(false);
            var latestRelease = releases.First(x => includePrerelease || !x.Prerelease);
            return latestRelease.TagName;
        }

        var allCommits = await GetAllCommits(installedPackage.Version.InstalledBranch)
            .ConfigureAwait(false);
        var latestCommit = allCommits.First();
        return latestCommit.Sha;
    }

    private async Task RunInvokeCommand(string installedPackagePath, string command,
        string arguments, bool runDetached)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        arguments = command switch
        {
            "invokeai-configure" => "--yes --skip-sd-weights",
            "invokeai-model-install" => "--yes",
            _ => arguments
        };

        VenvRunner.EnvironmentVariables = GetEnvVars(installedPackagePath);

        // Launch command is for a console entry point, and not a direct script
        var entryPoint = await VenvRunner.GetEntryPoint(command).ConfigureAwait(false);

        // Split at ':' to get package and function
        var split = entryPoint?.Split(':');

        if (split is not {Length: > 1})
        {
            throw new Exception($"Could not find entry point for InvokeAI: {entryPoint.ToRepr()}");
        }

        // Compile a startup command according to 
        // https://packaging.python.org/en/latest/specifications/entry-points/#use-for-scripts
        // For invokeai, also patch the shutil.get_terminal_size function to return a fixed value
        // above the minimum in invokeai.frontend.install.widgets

        var code = $"""
                    import sys
                    from {split[0]} import {split[1]}
                    sys.exit({split[1]}())
                    """;

        if (runDetached)
        {
            void HandleConsoleOutput(ProcessOutput s)
            {
                OnConsoleOutput(s);

                if (!s.Text.Contains("running on", StringComparison.OrdinalIgnoreCase)) 
                    return;
            
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (!match.Success)
                    return;
            
                WebUrl = match.Value;
                OnStartupComplete(WebUrl);
            }
            
            VenvRunner.RunDetached($"-c \"{code}\" {arguments}".TrimEnd(), HandleConsoleOutput, OnExit);
        }
        else
        {
            var result = await VenvRunner.Run($"-c \"{code}\" {arguments}".TrimEnd())
                .ConfigureAwait(false);
            OnConsoleOutput(new ProcessOutput
            {
                Text = result.StandardOutput
            });
        }
    }

    private Dictionary<string, string> GetEnvVars(DirectoryPath installPath)
    {
        // Set additional required environment variables
        var env = new Dictionary<string, string>();
        if (SettingsManager.Settings.EnvironmentVariables is not null)
        {
            env.Update(SettingsManager.Settings.EnvironmentVariables);
        }

        // Need to make subdirectory because they store config in the
        // directory *above* the root directory
        var root = installPath.JoinDir(RelativeRootPath);
        root.Create();
        env["INVOKEAI_ROOT"] = root;

        return env;
    }
}
