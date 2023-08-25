using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class VladAutomatic : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    public override string Name => "automatic";
    public override string DisplayName { get; set; } = "SD.Next Web UI";
    public override string Author => "vladmandic";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl => 
        "https://github.com/vladmandic/automatic/blob/master/LICENSE.txt";
    public override string Blurb => "Stable Diffusion implementation with advanced features";
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
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = new[] {"models/Stable-diffusion"},
        [SharedFolderType.Diffusers] = new[] {"models/Diffusers"},
        [SharedFolderType.VAE] = new[] {"models/VAE"},
        [SharedFolderType.TextualInversion] = new[] {"models/embeddings"},
        [SharedFolderType.Hypernetwork] = new[] {"models/hypernetworks"},
        [SharedFolderType.Codeformer] = new[] {"models/Codeformer"},
        [SharedFolderType.GFPGAN] = new[] {"models/GFPGAN"},
        [SharedFolderType.BSRGAN] = new[] {"models/BSRGAN"},
        [SharedFolderType.ESRGAN] = new[] {"models/ESRGAN"},
        [SharedFolderType.RealESRGAN] = new[] {"models/RealESRGAN"},
        [SharedFolderType.ScuNET] = new[] {"models/ScuNET"},
        [SharedFolderType.SwinIR] = new[] {"models/SwinIR"},
        [SharedFolderType.LDSR] = new[] {"models/LDSR"},
        [SharedFolderType.CLIP] = new[] {"models/CLIP"},
        [SharedFolderType.Lora] = new[] {"models/Lora"},
        [SharedFolderType.LyCORIS] = new[] {"models/LyCORIS"},
        [SharedFolderType.ControlNet] = new[] {"models/ControlNet"}
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
            Type = LaunchOptionType.Bool,
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
            Type = LaunchOptionType.Bool,
            Options = new() { "--use-ipex" }
        },
        new()
        {
            Name = "Use DirectML if no compatible GPU is detected",
            Type = LaunchOptionType.Bool,
            InitialValue = HardwareHelper.PreferDirectML(),
            Options = new() { "--use-directml" }
        },
        new()
        {
            Name = "Force use of Nvidia CUDA backend",
            Type = LaunchOptionType.Bool,
            InitialValue = HardwareHelper.HasNvidiaGpu(),
            Options = new() { "--use-cuda" }
        },
        new()
        {
            Name = "Force use of AMD ROCm backend",
            Type = LaunchOptionType.Bool,
            InitialValue = HardwareHelper.PreferRocm(),
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
            Type = LaunchOptionType.Bool,
            Options = new() { "--api" }
        },
        new()
        {
            Name = "Debug Logging",
            Type = LaunchOptionType.Bool,
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
        progress?.Report(new ProgressReport(-1f, "Installing package...", isIndeterminate: true));
        // Setup venv
        var venvRunner = new PyVenvRunner(Path.Combine(InstallLocation, "venv"));
        venvRunner.WorkingDirectory = InstallLocation;
        venvRunner.EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables;
        
        await venvRunner.Setup().ConfigureAwait(false);

        // Run initial install
        if (HardwareHelper.HasNvidiaGpu())
        {
            // CUDA
            await venvRunner.CustomInstall("launch.py --use-cuda --debug --test", OnConsoleOutput)
                .ConfigureAwait(false);
        }
        else if (HardwareHelper.PreferRocm())
        {
            // ROCm
            await venvRunner.CustomInstall("launch.py --use-rocm --debug --test", OnConsoleOutput)
                .ConfigureAwait(false);
        }
        else if (HardwareHelper.PreferDirectML())
        {
            // DirectML
            await venvRunner.CustomInstall("launch.py --use-directml --debug --test", OnConsoleOutput)
                .ConfigureAwait(false);
        }
        else
        {
            await venvRunner.CustomInstall("launch.py --debug --test", OnConsoleOutput)
                .ConfigureAwait(false);
        }
        
        progress?.Report(new ProgressReport(1, isIndeterminate: false));
    }

    public override async Task<string> DownloadPackage(string version, bool isCommitHash,
        string? branch, IProgress<ProgressReport>? progress = null)
    {
        progress?.Report(new ProgressReport(-1f, message: "Downloading package...",
            isIndeterminate: true, type: ProgressType.Download));

        var installDir = new DirectoryPath(InstallLocation);
        installDir.Create();

        await PrerequisiteHelper
            .RunGit(installDir.Parent ?? "", "clone", "https://github.com/vladmandic/automatic",
                installDir.Name).ConfigureAwait(false);

        await PrerequisiteHelper.RunGit(
            InstallLocation, "checkout", version).ConfigureAwait(false);

        return version;
    }

    public override async Task RunPackage(string installedPackagePath, string command, string arguments)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        void HandleConsoleOutput(ProcessOutput s)
        {
            OnConsoleOutput(s);
            if (s.Text.Contains("Running on local URL", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (match.Success)
                {
                    WebUrl = match.Value;
                    OnStartupComplete(WebUrl);
                }
            }
        }

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnExit(i);
        }

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, HandleExit);
    }

    public override async Task<string> Update(InstalledPackage installedPackage,
        IProgress<ProgressReport>? progress = null, bool includePrerelease = false)
    {
        if (installedPackage.InstalledBranch is null)
        {
            throw new Exception("Installed branch is null");
        }
        
        progress?.Report(new ProgressReport(-1f, message: "Downloading package update...",
            isIndeterminate: true, type: ProgressType.Update));

        await PrerequisiteHelper.RunGit(installedPackage.FullPath, "checkout",
            installedPackage.InstalledBranch).ConfigureAwait(false);
        
        var venvRunner = new PyVenvRunner(Path.Combine(installedPackage.FullPath!, "venv"));
        venvRunner.WorkingDirectory = InstallLocation;
        venvRunner.EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables;
        
        await venvRunner.CustomInstall("launch.py --upgrade --test", OnConsoleOutput)
            .ConfigureAwait(false);


        try
        {
            var output =
                await PrerequisiteHelper
                    .GetGitOutput(installedPackage.FullPath, "rev-parse", "HEAD")
                    .ConfigureAwait(false);

            return output.Replace(Environment.NewLine, "").Replace("\n", "");
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Could not get current git hash, continuing with update");
        }
        finally
        {
            progress?.Report(new ProgressReport(1f, message: "Update Complete", isIndeterminate: false,
                type: ProgressType.Update));
        }
        
        return installedPackage.InstalledBranch;
    }
}
