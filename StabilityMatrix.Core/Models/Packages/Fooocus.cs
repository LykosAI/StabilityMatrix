using System.Diagnostics;
using System.Text.RegularExpressions;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class Fooocus : BaseGitPackage
{
    public Fooocus(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper
    )
        : base(githubApi, settingsManager, downloadService, prerequisiteHelper) { }

    public override string Name => "Fooocus";
    public override string DisplayName { get; set; } = "Fooocus";
    public override string Author => "lllyasviel";

    public override string Blurb =>
        "Fooocus is a rethinking of Stable Diffusion and Midjourney’s designs";

    public override string LicenseType => "GPL-3.0";
    public override string LicenseUrl => "https://github.com/lllyasviel/Fooocus/blob/main/LICENSE";
    public override string LaunchCommand => "launch.py";

    public override Uri PreviewImageUri =>
        new(
            "https://user-images.githubusercontent.com/19834515/261830306-f79c5981-cf80-4ee3-b06b-3fef3f8bfbc7.png"
        );

    public override List<LaunchOptionDefinition> LaunchOptions =>
        new()
        {
            new LaunchOptionDefinition
            {
                Name = "Preset",
                Type = LaunchOptionType.Bool,
                Options = { "--preset anime", "--preset realistic" }
            },
            new LaunchOptionDefinition
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                Description = "Sets the listen port",
                Options = { "--port" }
            },
            new LaunchOptionDefinition
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = { "--share" }
            },
            new LaunchOptionDefinition
            {
                Name = "Listen",
                Type = LaunchOptionType.String,
                Description = "Set the listen interface",
                Options = { "--listen" }
            },
            new LaunchOptionDefinition
            {
                Name = "Output Directory",
                Type = LaunchOptionType.String,
                Description = "Override the output directory",
                Options = { "--output-directory" }
            },
            new()
            {
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper
                    .IterGpuInfo()
                    .Select(gpu => gpu.MemoryLevel)
                    .Max() switch
                {
                    Level.Low => "--lowvram",
                    Level.Medium => "--normalvram",
                    _ => null
                },
                Options = { "--highvram", "--normalvram", "--lowvram", "--novram" }
            },
            new LaunchOptionDefinition
            {
                Name = "Use DirectML",
                Type = LaunchOptionType.Bool,
                Description = "Use pytorch with DirectML support",
                InitialValue = HardwareHelper.PreferDirectML(),
                Options = { "--directml" }
            },
            new LaunchOptionDefinition
            {
                Name = "Disable Xformers",
                Type = LaunchOptionType.Bool,
                InitialValue = !HardwareHelper.HasNvidiaGpu(),
                Options = { "--disable-xformers" }
            },
            new LaunchOptionDefinition
            {
                Name = "Auto-Launch",
                Type = LaunchOptionType.Bool,
                Options = { "--auto-launch" }
            },
            LaunchOptionDefinition.Extras
        };

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.None };

    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/checkpoints" },
            [SharedFolderType.Diffusers] = new[] { "models/diffusers" },
            [SharedFolderType.Lora] = new[] { "models/loras" },
            [SharedFolderType.CLIP] = new[] { "models/clip" },
            [SharedFolderType.TextualInversion] = new[] { "models/embeddings" },
            [SharedFolderType.VAE] = new[] { "models/vae" },
            [SharedFolderType.ApproxVAE] = new[] { "models/vae_approx" },
            [SharedFolderType.ControlNet] = new[] { "models/controlnet" },
            [SharedFolderType.GLIGEN] = new[] { "models/gligen" },
            [SharedFolderType.ESRGAN] = new[] { "models/upscale_models" },
            [SharedFolderType.Hypernetwork] = new[] { "models/hypernetworks" }
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Text2Img] = new[] { "outputs" } };

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[] { TorchVersion.Cpu, TorchVersion.Cuda, TorchVersion.DirectMl, TorchVersion.Rocm };

    public override Task<string> GetLatestVersion() => Task.FromResult("main");

    public override bool ShouldIgnoreReleases => true;

    public override string OutputFolderName => "outputs";

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Simple;

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        var venvRunner = await SetupVenv(installLocation, forceRecreate: true)
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing torch...", isIndeterminate: true));

        if (torchVersion == TorchVersion.DirectMl)
        {
            await venvRunner
                .PipInstall(new PipInstallArgs().WithTorchDirectML(), onConsoleOutput)
                .ConfigureAwait(false);
        }
        else
        {
            var extraIndex = torchVersion switch
            {
                TorchVersion.Cpu => "cpu",
                TorchVersion.Cuda => "cu121",
                TorchVersion.Rocm => "rocm5.4.2",
                _ => throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null)
            };

            await venvRunner
                .PipInstall(
                    new PipInstallArgs()
                        .WithTorch("==2.1.0")
                        .WithTorchVision("==0.16.0")
                        .WithTorchExtraIndex(extraIndex),
                    onConsoleOutput
                )
                .ConfigureAwait(false);
        }

        var requirements = new FilePath(installLocation, "requirements_versions.txt");
        await venvRunner
            .PipInstallFromRequirements(requirements, onConsoleOutput, excludes: "torch")
            .ConfigureAwait(false);
    }

    public override async Task RunPackage(
        string installedPackagePath,
        string command,
        string arguments,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (s.Text.Contains("Use the app with", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
                var match = regex.Match(s.Text);
                if (match.Success)
                {
                    WebUrl = match.Value;
                }
                OnStartupComplete(WebUrl);
            }
        }

        void HandleExit(int i)
        {
            Debug.WriteLine($"Venv process exited with code {i}");
            OnExit(i);
        }

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner?.RunDetached(args.TrimEnd(), HandleConsoleOutput, HandleExit);
    }
}
