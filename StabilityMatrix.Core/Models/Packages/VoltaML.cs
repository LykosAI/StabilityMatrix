using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

public class VoltaML : BaseGitPackage
{
    public override string Name => "voltaML-fast-stable-diffusion";
    public override string DisplayName { get; set; } = "VoltaML";
    public override string Author => "VoltaML";
    public override string Blurb => "Fast Stable Diffusion with support for AITemplate";
    public override string LaunchCommand => "main.py";

    public override Uri PreviewImageUri => new(
        "https://github.com/LykosAI/StabilityMatrix/assets/13956642/d9a908ed-5665-41a5-a380-98458f4679a8");
    
    // There are releases but the manager just downloads the latest commit anyways,
    // so we'll just limit to commit mode to be more consistent
    public override bool ShouldIgnoreReleases => true;
    
    public VoltaML(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper) : 
        base(githubApi, settingsManager, downloadService, prerequisiteHelper)
    {
    }

    // https://github.com/VoltaML/voltaML-fast-stable-diffusion/blob/main/main.py#L86
    public override Dictionary<SharedFolderType, string> SharedFolders => new()
    {
        [SharedFolderType.StableDiffusion] = "data/models",
        [SharedFolderType.Lora] = "data/lora",
        [SharedFolderType.TextualInversion] = "data/textual-inversion",
    };
    
    // https://github.com/VoltaML/voltaML-fast-stable-diffusion/blob/main/main.py#L45
    public override List<LaunchOptionDefinition> LaunchOptions => new List<LaunchOptionDefinition>
    {
        new()
        {
            Name = "Log Level",
            Type = LaunchOptionType.Bool,
            DefaultValue = "--log-level INFO",
            Options =
            {
                "--log-level DEBUG", 
                "--log-level INFO", 
                "--log-level WARNING", 
                "--log-level ERROR",
                "--log-level CRITICAL"
            }
        },
        new()
        {
            Name = "Use ngrok to expose the API",
            Type = LaunchOptionType.Bool,
            Options = {"--ngrok"}
        },
        new()
        {
            Name = "Expose the API to the network",
            Type = LaunchOptionType.Bool,
            Options = {"--host"}
        },
        new()
        {
            Name = "Skip virtualenv check",
            Type = LaunchOptionType.Bool,
            InitialValue = true,
            Options = {"--in-container"}
        },
        new()
        {
            Name = "Force VoltaML to use a specific type of PyTorch distribution",
            Type = LaunchOptionType.Bool,
            Options =
            {
                "--pytorch-type cpu", 
                "--pytorch-type cuda", 
                "--pytorch-type rocm", 
                "--pytorch-type directml",
                "--pytorch-type intel",
                "--pytorch-type vulkan"
            }
        },
        new()
        {
            Name = "Run in tandem with the Discord bot",
            Type = LaunchOptionType.Bool,
            Options = {"--bot"}
        },
        new()
        {
            Name = "Enable Cloudflare R2 bucket upload support",
            Type = LaunchOptionType.Bool,
            Options = {"--enable-r2"}
        },
        new()
        {
            Name = "Port",
            Type = LaunchOptionType.String,
            DefaultValue = "5003",
            Options = {"--port"}
        },
        new()
        {
            Name = "Only install requirements and exit",
            Type = LaunchOptionType.Bool,
            Options = {"--install-only"}
        },
        LaunchOptionDefinition.Extras
    };

    public override Task<string> GetLatestVersion() => Task.FromResult("main");
    
    public override async Task InstallPackage(IProgress<ProgressReport>? progress = null)
    {
        await UnzipPackage(progress).ConfigureAwait(false);
        
        // Setup venv
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        using var venvRunner = new PyVenvRunner(Path.Combine(InstallLocation, "venv"));
        await venvRunner.Setup().ConfigureAwait(false);
        
        // Install requirements
        progress?.Report(new ProgressReport(-1, "Installing Package Requirements", isIndeterminate: true));
        await venvRunner
            .PipInstall("rich packaging python-dotenv", InstallLocation, OnConsoleOutput)
            .ConfigureAwait(false);
        
        progress?.Report(new ProgressReport(1, "Installing Package Requirements", isIndeterminate: false));
    }
    
    public override async Task RunPackage(string installedPackagePath, string arguments)
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        var args = $"\"{Path.Combine(installedPackagePath, LaunchCommand)}\" {arguments}";
        
        VenvRunner?.RunDetached(
            args.TrimEnd(),
            outputDataReceived: OnConsoleOutput, 
            onExit: OnExit, 
            workingDirectory: installedPackagePath,
            environmentVariables: SettingsManager.Settings.EnvironmentVariables);
    }
}
