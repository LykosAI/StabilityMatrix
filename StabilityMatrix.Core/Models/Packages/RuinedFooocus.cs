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

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        SharedFolderMethod selectedSharedFolderMethod,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        if (torchVersion == TorchVersion.Cuda)
        {
            var venvRunner = await SetupVenv(installLocation, forceRecreate: true).ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Installing requirements...", isIndeterminate: true));

            var requirements = new FilePath(installLocation, "requirements_versions.txt");

            await venvRunner
                .PipInstall(
                    new PipInstallArgs()
                        .WithTorch("==2.0.1")
                        .WithTorchVision("==0.15.2")
                        .WithXFormers("==0.0.20")
                        .WithTorchExtraIndex("cu118")
                        .WithParsedFromRequirementsTxt(
                            await requirements.ReadAllTextAsync().ConfigureAwait(false),
                            excludePattern: "torch"
                        ),
                    onConsoleOutput
                )
                .ConfigureAwait(false);
        }
        else
        {
            await base.InstallPackage(
                installLocation,
                torchVersion,
                selectedSharedFolderMethod,
                versionOptions,
                progress,
                onConsoleOutput
            )
                .ConfigureAwait(false);
        }

        // Create output folder since it's not created by default
        var outputFolder = new DirectoryPath(installLocation, OutputFolderName);
        outputFolder.Create();
    }
}
