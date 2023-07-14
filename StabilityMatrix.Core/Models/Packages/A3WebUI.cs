using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class A3WebUI : BaseGitPackage
{
    public override string Name => "stable-diffusion-webui";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI";
    public override string Author => "AUTOMATIC1111";

    public override string Blurb =>
        "A browser interface based on Gradio library for Stable Diffusion";
    public override string LaunchCommand => "launch.py";
    public override Uri PreviewImageUri =>
        new("https://github.com/AUTOMATIC1111/stable-diffusion-webui/raw/master/screenshot.png");
    public string RelativeArgsDefinitionScriptPath => "modules.cmd_args";


    public A3WebUI(IGithubApiCache githubApi, ISettingsManager settingsManager, IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) :
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }

    // From https://github.com/AUTOMATIC1111/stable-diffusion-webui/tree/master/models
    public override Dictionary<SharedFolderType, string> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = "models/Stable-diffusion",
        [SharedFolderType.ESRGAN] = "models/ESRGAN",
        [SharedFolderType.RealESRGAN] = "models/RealESRGAN",
        [SharedFolderType.SwinIR] = "models/SwinIR",
        [SharedFolderType.Lora] = "models/Lora",
        [SharedFolderType.LyCORIS] = "models/LyCORIS",
        [SharedFolderType.ApproxVAE] = "models/VAE-approx",
        [SharedFolderType.VAE] = "models/VAE",
        [SharedFolderType.DeepDanbooru] = "models/deepbooru",
        [SharedFolderType.Karlo] = "models/karlo",
        [SharedFolderType.TextualInversion] = "embeddings",
        [SharedFolderType.Hypernetwork] = "models/hypernetworks",
        [SharedFolderType.ControlNet] = "models/ControlNet"
    };

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new()
        {
            Name = "Host",
            Type = LaunchOptionType.String,
            DefaultValue = "localhost",
            Options = new() {"--host"}
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
            Name = "Xformers",
            Type = LaunchOptionType.Bool,
            InitialValue = HardwareHelper.HasNvidiaGpu(),
            Options = new() { "--xformers" }
        },
        new()
        {
            Name = "API",
            Type = LaunchOptionType.Bool,
            InitialValue = true,
            Options = new() {"--api"}
        },
        new()
        {
            Name = "Skip Torch CUDA Check",
            Type = LaunchOptionType.Bool,
            InitialValue = !HardwareHelper.HasNvidiaGpu(),
            Options = new() {"--skip-torch-cuda-test"}
        },
        new()
        {
            Name = "Skip Python Version Check",
            Type = LaunchOptionType.Bool,
            InitialValue = true,
            Options = new() {"--skip-python-version-check"}
        },
        LaunchOptionDefinition.Extras
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
            return allReleases.Where(r => r.Prerelease == false).Select(r => new PackageVersion
                {TagName = r.TagName!, ReleaseNotesMarkdown = r.Body});
        }

        // else, branch mode
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
        await PrerequisiteHelper.SetupPythonDependencies(InstallLocation, "requirements_versions.txt", progress,
            OnConsoleOutput);
    }

    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath);
        PrerequisiteHelper.UpdatePathExtensions();

        void HandleConsoleOutput(ProcessOutput s)
        {
            OnConsoleOutput(s);
            
            if (s.Text.Contains("model loaded", StringComparison.OrdinalIgnoreCase))
            {
                OnStartupComplete(WebUrl);
            }

            if (s.Text.Contains("Running on", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (match.Success)
                {
                    WebUrl = match.Value;
                }
            }
        }

        var args = $"\"{Path.Combine(installedPackagePath, LaunchCommand)}\" {arguments}";

        VenvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, OnExit, workingDirectory: installedPackagePath);
    }
}
