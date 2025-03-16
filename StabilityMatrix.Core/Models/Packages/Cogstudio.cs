using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Cogstudio>(Duplicate = DuplicateStrategy.Append)]
public class Cogstudio(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
)
    : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager),
        ISharedFolderLayoutPackage
{
    public override string Name => "Cogstudio";
    public override string DisplayName { get; set; } = "Cogstudio";
    public override string RepositoryName => "CogVideo";
    public override string RepositoryAuthor => "THUDM";
    public override string Author => "pinokiofactory";
    public override string Blurb =>
        "An advanced gradio web ui for generating and editing videos with CogVideo.";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/THUDM/CogVideo/blob/main/LICENSE";
    public override string LaunchCommand => "inference/gradio_composite_demo/cogstudio.py";
    public override Uri PreviewImageUri =>
        new("https://raw.githubusercontent.com/pinokiofactory/cogstudio/main/img2vid.gif");
    public override List<LaunchOptionDefinition> LaunchOptions => new() { LaunchOptionDefinition.Extras };
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.None };
    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        ((ISharedFolderLayoutPackage)this).LegacySharedFolders;
    public virtual SharedFolderLayout SharedFolderLayout => new();
    public override Dictionary<SharedOutputType, IReadOnlyList<string>> SharedOutputFolders =>
        new() { [SharedOutputType.Text2Vid] = new[] { "output" } };
    public override IEnumerable<TorchIndex> AvailableTorchIndices =>
        new[] { TorchIndex.Cpu, TorchIndex.Cuda };
    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;
    public override string OutputFolderName => "output";
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Simple;

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        const string cogstudioUrl =
            "https://raw.githubusercontent.com/pinokiofactory/cogstudio/refs/heads/main/cogstudio.py";

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Setting up Cogstudio files", isIndeterminate: true));
        var gradioCompositeDemo = new FilePath(installLocation, "inference/gradio_composite_demo");
        var cogstudioFile = new FilePath(gradioCompositeDemo, "cogstudio.py");
        gradioCompositeDemo.Directory?.Create();
        await DownloadService
            .DownloadToFileAsync(cogstudioUrl, cogstudioFile, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(
                -1f,
                "Patching cogstudio.py to allow writing to the output folder",
                isIndeterminate: true
            )
        );
        var outputDir = new FilePath(installLocation, "output");
        if (Compat.IsWindows)
        {
            outputDir = outputDir.ToString().Replace("\\", "\\\\");
        }
        var cogstudioContent = await cogstudioFile.ReadAllTextAsync(cancellationToken).ConfigureAwait(false);
        cogstudioContent = cogstudioContent.Replace(
            "demo.launch()",
            $"demo.launch(allowed_paths=['{outputDir}'])"
        );
        await cogstudioFile.WriteAllTextAsync(cogstudioContent, cancellationToken).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing requirements", isIndeterminate: true));
        var requirements = new FilePath(installLocation, "requirements.txt");
        var pipArgs = new PipInstallArgs()
            .WithTorch("==2.3.1")
            .WithTorchVision("==0.18.1")
            .WithTorchAudio("==2.3.1")
            .WithTorchExtraIndex("cu121")
            .WithParsedFromRequirementsTxt(
                await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
                excludePattern: Compat.IsWindows
                    ? "torch.*|moviepy.*|SwissArmyTransformer.*"
                    : "torch.*|moviepy.*"
            );

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        // SwissArmyTransformer is not available on Windows and DeepSpeed needs prebuilt wheels
        if (Compat.IsWindows)
        {
            await venvRunner
                .PipInstall(
                    " https://github.com/daswer123/deepspeed-windows/releases/download/11.2/deepspeed-0.11.2+cuda121-cp310-cp310-win_amd64.whl",
                    onConsoleOutput
                )
                .ConfigureAwait(false);
            await venvRunner
                .PipInstall("spandrel opencv-python scikit-video", onConsoleOutput)
                .ConfigureAwait(false);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        await venvRunner.PipInstall("moviepy==2.0.0.dev2", onConsoleOutput).ConfigureAwait(false);
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

            if (s.Text.Contains("Running on local URL", StringComparison.OrdinalIgnoreCase))
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
