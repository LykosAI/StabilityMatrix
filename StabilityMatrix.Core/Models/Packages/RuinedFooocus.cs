using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class RuinedFooocus(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : Fooocus(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "RuinedFooocus";
    public override string DisplayName { get; set; } = "RuinedFooocus";
    public override string Author => "runew0lf";
    public override string Blurb =>
        "RuinedFooocus combines the best aspects of Stable Diffusion and Midjourney into one seamless, cutting-edge experience";
    public override string LicenseUrl => "https://github.com/runew0lf/RuinedFooocus/blob/main/LICENSE";
    public override Uri PreviewImageUri =>
        new("https://raw.githubusercontent.com/runew0lf/pmmconfigs/main/RuinedFooocus_ss.png");
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Expert;
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.Symlink, SharedFolderMethod.None];
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

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
                InitialValue = HardwareHelper.IterGpuInfo().Select(gpu => gpu.MemoryLevel).Max() switch
                {
                    MemoryLevel.Low => "--lowvram",
                    MemoryLevel.Medium => "--normalvram",
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

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var torchVersion = options.PythonOptions.TorchVersion ?? GetRecommendedTorchVersion();

        if (torchVersion == TorchVersion.Cuda)
        {
            await using var venvRunner = await SetupVenvPure(installLocation, forceRecreate: true)
                .ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

            var requirements = new FilePath(installLocation, "requirements_versions.txt");

            await venvRunner
                .PipInstall(
                    new PipInstallArgs()
                        .WithParsedFromRequirementsTxt(
                            await requirements.ReadAllTextAsync().ConfigureAwait(false),
                            excludePattern: "torch"
                        )
                        .WithTorchExtraIndex("cu121"),
                    onConsoleOutput
                )
                .ConfigureAwait(false);
        }
        else
        {
            await base.InstallPackage(
                installLocation,
                installedPackage,
                options,
                progress,
                onConsoleOutput,
                cancellationToken
            )
                .ConfigureAwait(false);
        }

        // Create output folder since it's not created by default
        var outputFolder = new DirectoryPath(installLocation, OutputFolderName);
        outputFolder.Create();
    }
}
