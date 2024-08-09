using System.Text.RegularExpressions;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class VoltaML(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "voltaML-fast-stable-diffusion";
    public override string DisplayName { get; set; } = "VoltaML";
    public override string Author => "VoltaML";
    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl =>
        "https://github.com/VoltaML/voltaML-fast-stable-diffusion/blob/main/License";
    public override string Blurb => "Fast Stable Diffusion with support for AITemplate";
    public override string LaunchCommand => "main.py";

    public override Uri PreviewImageUri =>
        new(
            "https://github.com/LykosAI/StabilityMatrix/assets/13956642/d9a908ed-5665-41a5-a380-98458f4679a8"
        );

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Simple;

    // There are releases but the manager just downloads the latest commit anyways,
    // so we'll just limit to commit mode to be more consistent
    public override bool ShouldIgnoreReleases => true;

    // https://github.com/VoltaML/voltaML-fast-stable-diffusion/blob/main/main.py#L86
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[] { "data/models" },
            [SharedFolderType.Lora] = new[] { "data/lora" },
            [SharedFolderType.TextualInversion] = new[] { "data/textual-inversion" },
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Text2Img] = new[] { "data/outputs/txt2img" },
            [SharedOutputType.Extras] = new[] { "data/outputs/extra" },
            [SharedOutputType.Img2Img] = new[] { "data/outputs/img2img" },
        };

    public override string OutputFolderName => "data/outputs";

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[] { TorchVersion.Cpu, TorchVersion.Cuda, TorchVersion.DirectMl };

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.None };

    // https://github.com/VoltaML/voltaML-fast-stable-diffusion/blob/main/main.py#L45
    public override List<LaunchOptionDefinition> LaunchOptions =>
        new List<LaunchOptionDefinition>
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
                Options = { "--ngrok" }
            },
            new()
            {
                Name = "Expose the API to the network",
                Type = LaunchOptionType.Bool,
                Options = { "--host" }
            },
            new()
            {
                Name = "Skip virtualenv check",
                Type = LaunchOptionType.Bool,
                InitialValue = true,
                Options = { "--in-container" }
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
                Options = { "--bot" }
            },
            new()
            {
                Name = "Enable Cloudflare R2 bucket upload support",
                Type = LaunchOptionType.Bool,
                Options = { "--enable-r2" }
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "5003",
                Options = { "--port" }
            },
            new()
            {
                Name = "Only install requirements and exit",
                Type = LaunchOptionType.Bool,
                Options = { "--install-only" }
            },
            LaunchOptionDefinition.Extras
        };

    public override string MainBranch => "main";

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        // Setup venv
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        // Install requirements
        progress?.Report(new ProgressReport(-1, "Installing Package Requirements", isIndeterminate: true));
        await venvRunner.PipInstall("rich packaging python-dotenv", onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1, "Installing Package Requirements", isIndeterminate: false));
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await SetupVenv(installLocation).ConfigureAwait(false);

        var foundIndicator = false;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (s.Text.Contains("running on", StringComparison.OrdinalIgnoreCase))
            {
                // Next line will have the Web UI URL, so set a flag & wait for that
                foundIndicator = true;
                return;
            }

            if (!foundIndicator)
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (!match.Success)
                return;

            WebUrl = match.Value;
            OnStartupComplete(WebUrl);
            foundIndicator = false;
        }

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), ..options.Arguments],
            HandleConsoleOutput,
            OnExit
        );
    }
}
