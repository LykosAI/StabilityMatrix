using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, SimpleSDXL>(Duplicate = DuplicateStrategy.Append)]
public class SimpleSDXL(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
)
    : Fooocus(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    public override string Name => "SimpleSDXL";
    public override string DisplayName { get; set; } = "SimpleSDXL";
    public override string Author => "metercai";
    public override string Blurb =>
        "Enhanced version of Fooocus for SDXL, more suitable for Chinese and Cloud. Supports Flux.";
    public override string LicenseUrl => "https://github.com/metercai/SimpleSDXL/blob/SimpleSDXL/LICENSE";
    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/simplesdxl/preview.webp");
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Expert;
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.Configuration, SharedFolderMethod.Symlink, SharedFolderMethod.None];
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Configuration;
    public override string MainBranch => "SimpleSDXL";
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();

    public override IReadOnlyList<PackageVulnerability> KnownVulnerabilities =>
        [
            new()
            {
                Id = "GHSA-qq8j-phpf-c63j",
                Title = "Undisclosed Data Collection and Remote Access in simpleai_base Dependency",
                Description =
                    "SimpleSDXL depends on simpleai_base which contains compiled Rust code with:\n"
                    + "- Undisclosed remote access functionality using rathole\n"
                    + "- Hidden system information gathering via concealed executable calls\n"
                    + "- Covert data upload to tokentm.net (blockchain-associated domain)\n"
                    + "- Undisclosed VPN functionality pointing to servers blocked by Chinese authorities\n\n"
                    + "This poses significant security and privacy risks as system information is uploaded without consent "
                    + "and the compiled nature of the code means the full extent of the remote access capabilities cannot be verified.",
                Severity = VulnerabilitySeverity.Critical,
                PublishedDate = DateTimeOffset.Parse("2025-01-11"),
                InfoUrl = new Uri("https://github.com/metercai/SimpleSDXL/issues/97"),
                AffectedVersions = ["*"], // Affects all versions
            },
        ];

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Preset",
                Description = "Apply specified UI preset.",
                Type = LaunchOptionType.Bool,
                Options =
                {
                    "--preset anime",
                    "--preset realistic",
                    "--preset Flux",
                    "--preset Kolors",
                    "--preset pony_v6",
                },
            },
            new()
            {
                Name = "Language",
                Type = LaunchOptionType.String,
                Description = "Translate UI using json files in [language] folder.",
                InitialValue = "en",
                Options = { "--language" },
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                Description = "Sets the listen port",
                Options = { "--port" },
            },
            new()
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = { "--share" },
            },
            new()
            {
                Name = "Listen",
                Type = LaunchOptionType.String,
                Description = "Set the listen interface",
                Options = { "--listen" },
            },
            new()
            {
                Name = "Disable preset download",
                Description = "Disables downloading models for presets",
                DefaultValue = false,
                Type = LaunchOptionType.Bool,
                Options = { "--disable-preset-download" },
            },
            new()
            {
                Name = "Theme",
                Description = "Launches the UI with light or dark theme",
                Type = LaunchOptionType.String,
                DefaultValue = "dark",
                Options = { "--theme" },
            },
            new()
            {
                Name = "Disable offload from VRAM",
                Description =
                    "Force loading models to vram when the unload can be avoided. Some Mac users may need this.",
                Type = LaunchOptionType.Bool,
                InitialValue = Compat.IsMacOS,
                Options = { "--disable-offload-from-vram" },
            },
            new()
            {
                Name = "Disable image log",
                Description = "Prevent writing images and logs to the outputs folder.",
                Type = LaunchOptionType.Bool,
                Options = { "--disable-image-log" },
            },
            new()
            {
                Name = "Disable metadata",
                Description = "Disables saving metadata to images.",
                Type = LaunchOptionType.Bool,
                Options = { "--disable-metadata" },
            },
            new()
            {
                Name = "Disable enhance output sorting",
                Description = "Disables enhance output sorting for final image gallery.",
                Type = LaunchOptionType.Bool,
                Options = { "--disable-enhance-output-sorting" },
            },
            new()
            {
                Name = "Enable auto describe image",
                Description = "Enables automatic description of uov and enhance image when prompt is empty",
                DefaultValue = true,
                Type = LaunchOptionType.Bool,
                Options = { "--enable-auto-describe-image" },
            },
            new()
            {
                Name = "Always download new models",
                Description = "Always download newer models.",
                DefaultValue = false,
                Type = LaunchOptionType.Bool,
                Options = { "--always-download-new-model" },
            },
            new()
            {
                Name = "Disable comfyd",
                Description = "Disable auto start comfyd server at launch",
                Type = LaunchOptionType.Bool,
                Options = { "--disable-comfyd" },
            },
            LaunchOptionDefinition.Extras,
        ];

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                forceRecreate: true,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));
        // Get necessary dependencies
        await venvRunner.PipInstall("--upgrade pip", onConsoleOutput).ConfigureAwait(false);
        await venvRunner.PipInstall("nvidia-pyindex pygit2", onConsoleOutput).ConfigureAwait(false);
        await venvRunner.PipInstall("facexlib cpm_kernels", onConsoleOutput).ConfigureAwait(false);

        if (Compat.IsWindows)
        {
            // Download and Install pre-built insightface
            const string wheelUrl =
                "https://github.com/Gourieff/Assets/raw/main/Insightface/insightface-0.7.3-cp310-cp310-win_amd64.whl";

            var wheelPath = new FilePath(installLocation, "insightface-0.7.3-cp310-cp310-win_amd64.whl");
            await DownloadService
                .DownloadToFileAsync(wheelUrl, wheelPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await venvRunner.PipInstall($"{wheelPath}", onConsoleOutput).ConfigureAwait(false);
            await wheelPath.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        var requirements = new FilePath(installLocation, "requirements_versions.txt");

        var pipArgs = new PipInstallArgs()
            .WithTorch("==2.3.1")
            .WithTorchVision("==0.18.1")
            .WithTorchAudio("==2.3.1")
            .WithTorchExtraIndex("cu121")
            .WithParsedFromRequirementsTxt(
                await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
                "torch"
            );

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        // Create output folder since it's not created by default
        var outputFolder = new DirectoryPath(installLocation, OutputFolderName);
        outputFolder.Create();
    }
}
