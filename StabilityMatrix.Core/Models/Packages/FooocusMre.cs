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
public class FooocusMre(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "Fooocus-MRE";
    public override string DisplayName { get; set; } = "Fooocus-MRE";
    public override string Author => "MoonRide303";

    public override string Blurb =>
        "Fooocus-MRE is an image generating software, enhanced variant of the original Fooocus dedicated for a bit more advanced users";

    public override string LicenseType => "GPL-3.0";

    public override string LicenseUrl =>
        "https://github.com/MoonRide303/Fooocus-MRE/blob/moonride-main/LICENSE";
    public override string LaunchCommand => "launch.py";

    public override Uri PreviewImageUri =>
        new(
            "https://user-images.githubusercontent.com/130458190/265366059-ce430ea0-0995-4067-98dd-cef1d7dc1ab6.png"
        );

    public override string Disclaimer =>
        "This package may no longer receive updates from its author. It may be removed from Stability Matrix in the future.";

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Impossible;
    public override bool OfferInOneClickInstaller => false;

    public override List<LaunchOptionDefinition> LaunchOptions =>
        new()
        {
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
        new[] { TorchVersion.Cpu, TorchVersion.Cuda, TorchVersion.Rocm };

    public override string MainBranch => "moonride-main";

    public override string OutputFolderName => "outputs";

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing torch...", isIndeterminate: true));

        var torchVersion = options.PythonOptions.TorchVersion ?? GetRecommendedTorchVersion();

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
                TorchVersion.Cuda => "cu118",
                TorchVersion.Rocm => "rocm5.4.2",
                _ => throw new ArgumentOutOfRangeException(nameof(torchVersion), torchVersion, null)
            };

            await venvRunner
                .PipInstall(
                    new PipInstallArgs()
                        .WithTorch("==2.0.1")
                        .WithTorchVision("==0.15.2")
                        .WithTorchExtraIndex(extraIndex),
                    onConsoleOutput
                )
                .ConfigureAwait(false);
        }

        var requirements = new FilePath(installLocation, "requirements_versions.txt");
        await venvRunner
            .PipInstall(
                new PipInstallArgs().WithParsedFromRequirementsTxt(
                    await requirements.ReadAllTextAsync().ConfigureAwait(false),
                    excludePattern: "torch"
                ),
                onConsoleOutput
            )
            .ConfigureAwait(false);
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

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), ..options.Arguments],
            HandleConsoleOutput,
            OnExit
        );
    }
}
