using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, AiToolkit>(Duplicate = DuplicateStrategy.Append)]
public class AiToolkit(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : BaseGitPackage(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    private AnsiProcess? npmProcess;

    public override string Name => "ai-toolkit";
    public override string DisplayName { get; set; } = "AI-Toolkit";
    public override string Author => "ostris";
    public override string Blurb => "AI Toolkit is an all in one training suite for diffusion models";
    public override string LicenseType => "MIT";
    public override string LicenseUrl => "https://github.com/ostris/ai-toolkit/blob/main/LICENSE";
    public override string LaunchCommand => string.Empty;

    public override Uri PreviewImageUri =>
        new(
            "https://camo.githubusercontent.com/ea35b399e0d659f9f2ee09cbedb58e1a3ec7a0eab763e8ae8d11d076aad5be40/68747470733a2f2f6f73747269732e636f6d2f77702d636f6e74656e742f75706c6f6164732f323032352f30322f746f6f6c6b69742d75692e6a7067"
        );

    public override string OutputFolderName => "output";
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;
    public override List<LaunchOptionDefinition> LaunchOptions => [];
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders => [];
    public override string MainBranch => "main";
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();

    public override TorchIndex GetRecommendedTorchVersion() => TorchIndex.Cuda;

    public override PackageType PackageType => PackageType.SdTraining;
    public override bool OfferInOneClickInstaller => false;
    public override bool ShouldIgnoreReleases => true;
    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;

    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.Node]);

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
        venvRunner.UpdateEnvironmentVariables(GetEnvVars);

        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        var isBlackwell =
            SettingsManager.Settings.PreferredGpu?.IsBlackwellGpu() ?? HardwareHelper.HasBlackwellGpu();
        var pipArgs = new PipInstallArgs()
            .AddArg("--upgrade")
            .WithTorch("==2.7.0")
            .WithTorchVision("==0.22.0")
            .WithTorchAudio("==2.7.0")
            .WithTorchExtraIndex(isBlackwell ? "cu128" : "cu126");

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        progress?.Report(new ProgressReport(-1f, "Installing torch...", isIndeterminate: true));
        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        // install requirements.txt
        var requirements = new FilePath(installLocation, "requirements.txt");

        pipArgs = new PipInstallArgs("--upgrade")
            .WithParsedFromRequirementsTxt(
                await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
                excludePattern: "torch"
            )
            .AddArg(Compat.IsWindows ? "triton-windows" : "triton")
            .WithTorchExtraIndex(isBlackwell ? "cu128" : "cu126");

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        progress?.Report(
            new ProgressReport(-1f, "Installing Package Requirements...", isIndeterminate: true)
        );
        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing AI Toolkit UI...", isIndeterminate: true));

        var uiDirectory = new DirectoryPath(installLocation, "ui");
        var envVars = GetEnvVars(venvRunner.EnvironmentVariables);
        await PrerequisiteHelper
            .RunNpm("install", uiDirectory, progress?.AsProcessOutputHandler(), envVars)
            .ConfigureAwait(false);
        await PrerequisiteHelper
            .RunNpm("run update_db", uiDirectory, progress?.AsProcessOutputHandler(), envVars)
            .ConfigureAwait(false);
        await PrerequisiteHelper
            .RunNpm("run build", uiDirectory, progress?.AsProcessOutputHandler(), envVars)
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
        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);
        VenvRunner.UpdateEnvironmentVariables(GetEnvVars);

        var uiDirectory = new DirectoryPath(installLocation, "ui");
        var envVars = GetEnvVars(VenvRunner.EnvironmentVariables);
        npmProcess = PrerequisiteHelper.RunNpmDetached(
            "run start",
            uiDirectory,
            HandleConsoleOutput,
            envVars
        );
        npmProcess.EnableRaisingEvents = true;
        if (Compat.IsWindows)
        {
            ProcessTracker.AttachExitHandlerJobToProcess(npmProcess);
        }

        return;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("Local:  ", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (match.Success)
            {
                WebUrl = match.Value;
            }
            OnStartupComplete(WebUrl);
        }
    }

    public override async Task WaitForShutdown()
    {
        if (npmProcess is { HasExited: false })
        {
            npmProcess.Kill(true);
            try
            {
                await npmProcess
                    .WaitForExitAsync(new CancellationTokenSource(5000).Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine(e);
            }
        }

        npmProcess = null;
        GC.SuppressFinalize(this);
    }

    private ImmutableDictionary<string, string> GetEnvVars(ImmutableDictionary<string, string> env)
    {
        var pathBuilder = new EnvPathBuilder();

        if (env.TryGetValue("PATH", out var value))
        {
            pathBuilder.AddPath(value);
        }

        pathBuilder.AddPath(
            Compat.IsWindows
                ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                : Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs", "bin")
        );

        pathBuilder.AddPath(Path.Combine(SettingsManager.LibraryDir, "Assets", "nodejs"));

        return env.SetItem("PATH", pathBuilder.ToString());
    }
}
