using System.Text.RegularExpressions;
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

[RegisterSingleton<BasePackage, KohyaSs>(Duplicate = DuplicateStrategy.Append)]
public class KohyaSs(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyRunner runner
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    public override string Name => "kohya_ss";
    public override string DisplayName { get; set; } = "kohya_ss";
    public override string Author => "bmaltais";
    public override string Blurb => "A Windows-focused Gradio GUI for Kohya's Stable Diffusion trainers";
    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/bmaltais/kohya_ss/blob/master/LICENSE.md";
    public override string LaunchCommand => "kohya_gui.py";
    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/kohyass/preview.webp");
    public override string OutputFolderName => string.Empty;
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();

    public override TorchIndex GetRecommendedTorchVersion() => TorchIndex.Cuda;

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.UltraNightmare;
    public override PackageType PackageType => PackageType.SdTraining;
    public override bool OfferInOneClickInstaller => false;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];
    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods => [SharedFolderMethod.None];
    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.Tkinter]);

    public override List<LaunchOptionDefinition> LaunchOptions =>
        [
            new LaunchOptionDefinition
            {
                Name = "Listen Address",
                Type = LaunchOptionType.String,
                DefaultValue = "127.0.0.1",
                Options = ["--listen"]
            },
            new LaunchOptionDefinition
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                Options = ["--port"]
            },
            new LaunchOptionDefinition
            {
                Name = "Username",
                Type = LaunchOptionType.String,
                Options = ["--username"]
            },
            new LaunchOptionDefinition
            {
                Name = "Password",
                Type = LaunchOptionType.String,
                Options = ["--password"]
            },
            new LaunchOptionDefinition
            {
                Name = "Auto-Launch Browser",
                Type = LaunchOptionType.Bool,
                Options = ["--inbrowser"]
            },
            new LaunchOptionDefinition
            {
                Name = "Share",
                Type = LaunchOptionType.Bool,
                Options = ["--share"]
            },
            new LaunchOptionDefinition
            {
                Name = "Headless",
                Type = LaunchOptionType.Bool,
                Options = ["--headless"]
            },
            new LaunchOptionDefinition
            {
                Name = "Language",
                Type = LaunchOptionType.String,
                Options = ["--language"]
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
        progress?.Report(new ProgressReport(-1f, "Updating submodules", isIndeterminate: true));
        await PrerequisiteHelper
            .RunGit(
                ["submodule", "update", "--init", "--recursive", "--quiet"],
                onConsoleOutput,
                installLocation
            )
            .ConfigureAwait(false);

        // make sure long paths are enabled
        if (Compat.IsWindows)
        {
            await PrerequisiteHelper.FixGitLongPaths().ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));

        // Setup venv
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);

        // Extra dep needed before running setup since v23.0.x
        var pipArgs = new PipInstallArgs("rich", "packaging", "setuptools", "uv");
        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs).ConfigureAwait(false);

        var isLegacyNvidia =
            SettingsManager.Settings.PreferredGpu?.IsLegacyNvidiaGpu() ?? HardwareHelper.HasLegacyNvidiaGpu();
        var torchExtraIndex = isLegacyNvidia ? "cu126" : "cu128";

        // install torch
        pipArgs = new PipInstallArgs()
            .WithTorch()
            .WithTorchVision()
            .WithTorchAudio()
            .WithXFormers()
            .WithTorchExtraIndex(torchExtraIndex)
            .AddArg("--force-reinstall");

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        if (Compat.IsLinux)
        {
            await venvRunner
                .CustomInstall(
                    [
                        "setup/setup_linux.py",
                        "--platform-requirements-file=requirements_linux.txt",
                        "--no_run_accelerate",
                    ],
                    onConsoleOutput
                )
                .ConfigureAwait(false);
            pipArgs = new PipInstallArgs();
        }
        else if (Compat.IsWindows)
        {
            var requirements = new FilePath(installLocation, "requirements_windows.txt");
            pipArgs = new PipInstallArgs()
                .WithParsedFromRequirementsTxt(
                    await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
                    "bitsandbytes==0\\.44\\.0"
                )
                .AddArg("bitsandbytes");
        }

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);
        await venvRunner.PipInstall("numpy==1.26.4", onConsoleOutput).ConfigureAwait(false);
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

            if (!s.Text.Contains("Running on", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (!match.Success)
                return;

            WebUrl = match.Value;
            OnStartupComplete(WebUrl);
        }

        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), ..options.Arguments],
            HandleConsoleOutput,
            OnExit
        );
    }

    public override Dictionary<SharedFolderType, IReadOnlyList<string>>? SharedFolders { get; }
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders { get; }

    public override string MainBranch => "master";
}
