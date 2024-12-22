using System.Diagnostics;
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

[RegisterSingleton<BasePackage, ComfyZluda>(Duplicate = DuplicateStrategy.Append)]
public class ComfyZluda(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper
) : ComfyUI(githubApi, settingsManager, downloadService, prerequisiteHelper)
{
    private const string ZludaPatchDownloadUrl =
        "https://github.com/lshqqytiger/ZLUDA/releases/download/rel.c0804ca624963aab420cb418412b1c7fbae3454b/ZLUDA-windows-rocm5-amd64.zip";
    private Process? zludaProcess;

    public override string Name => "ComfyUI-Zluda";
    public override string DisplayName => "ComfyUI-Zluda";
    public override string Author => "patientx";
    public override string LicenseUrl => "https://github.com/patientx/ComfyUI-Zluda/blob/master/LICENSE";
    public override string Blurb =>
        "Windows-only version of ComfyUI which uses ZLUDA to get better performance with AMD GPUs.";
    public override string Disclaimer =>
        "Installation of this package may require a reboot. Prerequisite install may require admin privileges.";
    public override string LaunchCommand => Path.Combine("zluda", "zluda.exe");
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Zluda];

    public override TorchIndex GetRecommendedTorchVersion() => TorchIndex.Zluda;

    public override bool IsCompatible => HardwareHelper.PreferDirectMLOrZluda();

    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.HipSdk]);

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
        await using var venvRunner = await SetupVenvPure(installLocation).ConfigureAwait(false);
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        var pipArgs = new PipInstallArgs()
            .WithTorch("==2.3.0")
            .WithTorchVision("==0.18.0")
            .WithTorchAudio("==2.3.0")
            .WithTorchExtraIndex("cu118");

        var requirements = new FilePath(installLocation, "requirements.txt");
        pipArgs = pipArgs.WithParsedFromRequirementsTxt(
            await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
            excludePattern: "torch$|numpy"
        );

        pipArgs = pipArgs.AddArg("numpy==1.26.0");

        progress?.Report(
            new ProgressReport(-1f, "Installing Package Requirements...", isIndeterminate: true)
        );
        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1, "Installed Package Requirements", isIndeterminate: false));

        progress?.Report(new ProgressReport(-1f, "Setting up ZLUDA...", isIndeterminate: true));

        // patch zluda
        var zludaPatchPath = new FilePath(installLocation, "zluda.zip");
        await downloadService
            .DownloadToFileAsync(
                ZludaPatchDownloadUrl,
                zludaPatchPath,
                progress,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        await ArchiveHelper.Extract(zludaPatchPath, installLocation, progress).ConfigureAwait(false);

        // copy some stuff into the venv
        var cublasSourcePath = new FilePath(installLocation, "zluda", "cublas.dll");
        var cusparseSourcePath = new FilePath(installLocation, "zluda", "cusparse.dll");
        var nvrtcSourcePath = new FilePath(installLocation, "zluda", "nvrtc.dll");
        var cublasDestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "cublas64_11.dll"
        );
        var cusparseDestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "cusparse64_11.dll"
        );
        var nvrtcDestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "nvrtc64_112_0.dll"
        );

        await cublasSourcePath.CopyToAsync(cublasDestPath, true).ConfigureAwait(false);
        await cusparseSourcePath.CopyToAsync(cusparseDestPath, true).ConfigureAwait(false);
        await nvrtcSourcePath.CopyToAsync(nvrtcDestPath, true).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1, "Installed ZLUDA", isIndeterminate: false));
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
        var portableGitBin = new DirectoryPath(PrerequisiteHelper.GitBinPath);
        var envVars = new Dictionary<string, string>
        {
            ["ZLUDA_COMGR_LOG_LEVEL"] = "1",
            ["HIP_PATH"] = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AMD",
                "ROCm",
                "5.7"
            ),
            ["GIT"] = portableGitBin.JoinFile("git.exe")
        };
        envVars.Update(settingsManager.Settings.EnvironmentVariables);

        if (envVars.TryGetValue("PATH", out var pathValue))
        {
            envVars["PATH"] = Compat.GetEnvPathWithExtensions(portableGitBin, pathValue);
        }
        else
        {
            envVars["PATH"] = Compat.GetEnvPathWithExtensions(portableGitBin);
        }

        var zludaPath = Path.Combine(installLocation, LaunchCommand);
        ProcessArgs args = ["--", VenvRunner.PythonPath.ToString(), "main.py", ..options.Arguments];
        zludaProcess = ProcessRunner.StartAnsiProcess(
            zludaPath,
            args,
            installLocation,
            HandleConsoleOutput,
            envVars
        );

        return;

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("To see the GUI go to", StringComparison.OrdinalIgnoreCase))
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
        if (zludaProcess is { HasExited: false })
        {
            zludaProcess.Kill(true);
            try
            {
                await zludaProcess
                    .WaitForExitAsync(new CancellationTokenSource(5000).Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine(e);
            }
        }

        zludaProcess = null;
        GC.SuppressFinalize(this);
    }
}
