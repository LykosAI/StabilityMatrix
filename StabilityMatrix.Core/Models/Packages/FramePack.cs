using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, FramePack>(Duplicate = DuplicateStrategy.Append)]
public class FramePack(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    public override string Name => "framepack";
    public override string DisplayName { get; set; } = "FramePack";
    public override string Author => "lllyasviel";

    public override string Blurb =>
        "FramePack is a next-frame (next-frame-section) prediction neural network structure that generates videos progressively.";

    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/lllyasviel/FramePack/blob/main/LICENSE";
    public override string LaunchCommand => "demo_gradio.py";
    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/framepack/framepack.png");
    public override string OutputFolderName => "outputs";
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new()
            {
                Name = "Server",
                Type = LaunchOptionType.String,
                DefaultValue = "0.0.0.0",
                InitialValue = "127.0.0.1",
                Options = ["--server"],
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = ["--port"],
            },
            new()
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Description = "Set whether to share on Gradio",
                Options = ["--share"],
            },
            new()
            {
                Name = "In Browser",
                Type = LaunchOptionType.Bool,
                Options = ["--inbrowser"],
                InitialValue = true,
            },
        ];

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new() { [SharedOutputType.Img2Vid] = ["outputs"] };

    public override string MainBranch => "main";
    public override bool ShouldIgnoreReleases => true;
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods => [SharedFolderMethod.None];
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();
    public override IReadOnlyList<string> ExtraLaunchArguments =>
        settingsManager.IsLibraryDirSet ? ["--gradio-allowed-paths", settingsManager.ImagesDirectory] : [];

    public override IReadOnlyDictionary<string, string> ExtraLaunchCommands =>
        new Dictionary<string, string> { ["FramePack F1"] = "demo_gradio_f1.py" };

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        var isLegacyNvidia =
            SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() ?? HardwareHelper.HasLegacyNvidiaGpu();
        var isNewerNvidia =
            SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu()
            ?? HardwareHelper.HasAmpereOrNewerGpu();

        var extraArgs = new List<string>();
        if (isNewerNvidia)
        {
            extraArgs.Add(Compat.IsWindows ? "triton-windows" : "triton");
        }

        var config = new PipInstallConfig
        {
            RequirementsFilePaths = ["requirements.txt"],
            TorchaudioVersion = " ", // Request torchaudio install
            XformersVersion = " ", // Request xformers install
            CudaIndex = isLegacyNvidia ? "cu126" : "cu128",
            UpgradePackages = true,
            ExtraPipArgs = extraArgs,
            PostInstallPipArgs = ["numpy==1.26.4"],
        };

        await StandardPipInstallProcessAsync(
                venvRunner,
                options,
                installedPackage,
                config,
                onConsoleOutput,
                progress,
                cancellationToken
            )
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(1, "Install complete", isIndeterminate: false));
    }

    public override async Task RunPackage(
        string installLocation,
        InstalledPackage installedPackage,
        RunPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);

        // Path to the original demo_gradio.py file
        var originalDemoPath = Path.Combine(installLocation, options.Command ?? LaunchCommand);
        var modifiedDemoPath = Path.Combine(installLocation, "demo_gradio_modified.py");

        // Read the original demo_gradio.py file
        var originalContent = await File.ReadAllTextAsync(originalDemoPath, cancellationToken)
            .ConfigureAwait(false);

        // Modify the content to add --gradio-allowed-paths support
        var modifiedContent = AddGradioAllowedPathsSupport(originalContent);

        // Write the modified content to a new file
        await File.WriteAllTextAsync(modifiedDemoPath, modifiedContent, cancellationToken)
            .ConfigureAwait(false);

        VenvRunner.RunDetached(
            [modifiedDemoPath, .. options.Arguments, .. ExtraLaunchArguments],
            HandleConsoleOutput,
            OnExit
        );

        return;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("Running on local URL", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (match.Success)
                WebUrl = match.Value;
            OnStartupComplete(WebUrl);
        }
    }

    public override List<ExtraPackageCommand> GetExtraCommands()
    {
        return Compat.IsWindows && SettingsManager.Settings.PreferredGpu?.IsAmpereOrNewerGpu() is true
            ?
            [
                new ExtraPackageCommand
                {
                    CommandName = "Install Triton and SageAttention",
                    Command = async installedPackage =>
                    {
                        if (installedPackage == null || string.IsNullOrEmpty(installedPackage.FullPath))
                            throw new InvalidOperationException(
                                "Package not found or not installed correctly"
                            );

                        await InstallTritonAndSageAttention(installedPackage).ConfigureAwait(false);
                    },
                },
            ]
            : [];
    }

    private static string AddGradioAllowedPathsSupport(string originalContent)
    {
        // Add the --gradio-allowed-paths argument to the argument parser
        var parserPattern =
            @"(parser\.add_argument\(""--inbrowser"", action='store_true'\)\s*\n)(args = parser\.parse_args\(\))";
        var parserReplacement =
            "$1parser.add_argument('--gradio-allowed-paths', nargs='*', default=[], help='Allowed paths for Gradio file access')\n$2";

        var modifiedContent = Regex.Replace(
            originalContent,
            parserPattern,
            parserReplacement,
            RegexOptions.Multiline
        );

        // Add the allowed_paths parameter to the block.launch() call
        var launchPattern =
            @"(block\.launch\(\s*\n\s*server_name=args\.server,\s*\n\s*server_port=args\.port,\s*\n\s*share=args\.share,\s*\n\s*inbrowser=args\.inbrowser,)\s*\n(\))";
        var launchReplacement = "$1\n    allowed_paths=args.gradio_allowed_paths,\n$2";

        modifiedContent = Regex.Replace(
            modifiedContent,
            launchPattern,
            launchReplacement,
            RegexOptions.Multiline
        );

        return modifiedContent;
    }

    private async Task InstallTritonAndSageAttention(InstalledPackage installedPackage)
    {
        if (installedPackage.FullPath is null)
            return;

        var installSageStep = new InstallSageAttentionStep(
            DownloadService,
            PrerequisiteHelper,
            PyInstallationManager
        )
        {
            InstalledPackage = installedPackage,
            WorkingDirectory = new DirectoryPath(installedPackage.FullPath),
            EnvironmentVariables = SettingsManager.Settings.EnvironmentVariables,
            IsBlackwellGpu =
                SettingsManager.Settings.PreferredGpu?.IsBlackwellGpu() ?? HardwareHelper.HasBlackwellGpu(),
        };

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            ModificationCompleteMessage = "Triton and SageAttention installed successfully",
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps([installSageStep]).ConfigureAwait(false);
    }
}
