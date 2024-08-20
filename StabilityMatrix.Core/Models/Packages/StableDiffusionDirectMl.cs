using NLog;
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
public class StableDiffusionDirectMl(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : A3WebUI(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public override string Name => "stable-diffusion-webui-directml";
    public override string DisplayName { get; set; } = "Stable Diffusion Web UI";
    public override string Author => "lshqqytiger";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl =>
        "https://github.com/lshqqytiger/stable-diffusion-webui-directml/blob/master/LICENSE.txt";
    public override string Blurb => "A fork of Automatic1111's Stable Diffusion WebUI with DirectML support";
    public override string LaunchCommand => "launch.py";
    public override Uri PreviewImageUri =>
        new("https://github.com/lshqqytiger/stable-diffusion-webui-directml/raw/master/screenshot.png");
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override TorchVersion GetRecommendedTorchVersion() =>
        HardwareHelper.PreferDirectML() ? TorchVersion.DirectMl : base.GetRecommendedTorchVersion();

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Recommended;

    public override List<LaunchOptionDefinition> LaunchOptions
    {
        get
        {
            var baseLaunchOptions = base.LaunchOptions;
            baseLaunchOptions.Insert(
                0,
                new LaunchOptionDefinition
                {
                    Name = "Use DirectML",
                    Type = LaunchOptionType.Bool,
                    InitialValue = HardwareHelper.PreferDirectML(),
                    Options = ["--use-directml"]
                }
            );

            return baseLaunchOptions;
        }
    }

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[] { TorchVersion.Cpu, TorchVersion.DirectMl };

    public override bool ShouldIgnoreReleases => true;

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        var torchVersion = options.PythonOptions.TorchVersion ?? GetRecommendedTorchVersion();
        switch (torchVersion)
        {
            case TorchVersion.DirectMl:
                await InstallDirectMlTorch(venvRunner, progress, onConsoleOutput).ConfigureAwait(false);
                break;
            case TorchVersion.Cpu:
                await InstallCpuTorch(venvRunner, progress, onConsoleOutput).ConfigureAwait(false);
                break;
        }

        await venvRunner.PipInstall("httpx==0.24.1").ConfigureAwait(false);

        // Install requirements file
        progress?.Report(new ProgressReport(-1f, "Installing Package Requirements", isIndeterminate: true));
        Logger.Info("Installing requirements_versions.txt");

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

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }
}
