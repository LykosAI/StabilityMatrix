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

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
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
            new()
            {
                Name = "Auth",
                Type = LaunchOptionType.String,
                Description = "Set credentials username/password",
                Options = { "--auth" }
            },
            new()
            {
                Name = "No Browser",
                Type = LaunchOptionType.Bool,
                Description = "Do not launch in browser",
                Options = { "--nobrowser" }
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
        var torchVersion = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        if (torchVersion == TorchIndex.Cuda)
        {
            await using var venvRunner = await SetupVenvPure(installLocation, forceRecreate: true)
                .ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

            var requirements = new FilePath(installLocation, "requirements_versions.txt");
            var pipArgs = new PipInstallArgs()
                .WithTorchExtraIndex("cu121")
                .WithParsedFromRequirementsTxt(
                    await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
                    "--extra-index-url.*|--index-url.*"
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
