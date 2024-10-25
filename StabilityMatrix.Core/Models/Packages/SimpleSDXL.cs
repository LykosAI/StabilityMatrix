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
public class SimpleSDXL(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : Fooocus(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "SimpleSDXL";
    public override string DisplayName { get; set; } = "SimpleSDXL";
    public override string Author => "metercai";
    public override string Blurb =>
        "Enhanced version of Fooocus for SDXL, more suitable for Chinese and Cloud. Supports Flux.";
    public override string LicenseUrl => "https://github.com/metercai/SimpleSDXL/blob/SimpleSDXL/LICENSE";
    public override Uri PreviewImageUri =>
        new("https://github.com/user-attachments/assets/98715a4d-9f4a-4846-ae62-eb8d69793d31");
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Expert;
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        [SharedFolderMethod.Symlink, SharedFolderMethod.None];
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;
    public override string MainBranch => "SimpleSDXL";

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Preset",
                Type = LaunchOptionType.Bool,
                Options =
                {
                    "--preset anime",
                    "--preset realistic",
                    "--preset Flux",
                    "--preset Kolors",
                    "--preset pony_v6"
                },
            },
            new()
            {
                Name = "Language",
                Type = LaunchOptionType.String,
                Description = "Change the language of the UI",
                Options = { "--language" },
                DefaultValue = "en"
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                Description = "Sets the listen port",
                Options = { "--port" }
            },
            new()
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = { "--share" }
            },
            new()
            {
                Name = "Listen",
                Type = LaunchOptionType.String,
                Description = "Set the listen interface",
                Options = { "--listen" }
            },
            LaunchOptionDefinition.Extras
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
        const string wheelUrl =
            "https://github.com/Gourieff/Assets/raw/main/Insightface/insightface-0.7.3-cp310-cp310-win_amd64.whl";
        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        if (torchVersion == TorchIndex.Cuda)
        {
            await using var venvRunner = await SetupVenvPure(installLocation, forceRecreate: true)
                .ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

            // Get necessary dependencies
            await venvRunner.PipInstall("--upgrade pip", onConsoleOutput).ConfigureAwait(false);
            await venvRunner.PipInstall("nvidia-pyindex pygit2", onConsoleOutput).ConfigureAwait(false);
            await venvRunner.PipInstall("facexlib cpm_kernels", onConsoleOutput).ConfigureAwait(false);

            // Download and Install pre-built insightface
            var wheelPath = new FilePath(installLocation, "insightface-0.7.3-cp310-cp310-win_amd64.whl");
            await downloadService
                .DownloadToFileAsync(wheelUrl, wheelPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await venvRunner.PipInstall($"{wheelPath}", onConsoleOutput).ConfigureAwait(false);
            await wheelPath.DeleteAsync(cancellationToken).ConfigureAwait(false);

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
