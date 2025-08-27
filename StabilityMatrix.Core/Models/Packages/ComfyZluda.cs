using System.Diagnostics;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Exceptions;
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
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : ComfyUI(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    private const string ZludaPatchDownloadUrl =
        "https://github.com/lshqqytiger/ZLUDA/releases/download/rel.5e717459179dc272b7d7d23391f0fad66c7459cf/ZLUDA-nightly-windows-rocm6-amd64.zip";

    private const string HipSdkExtensionDownloadUrl = "https://cdn.lykos.ai/HIP-SDK-extension.7z";

    private Process? zludaProcess;

    public override string Name => "ComfyUI-Zluda";
    public override string DisplayName => "ComfyUI-Zluda";
    public override string Author => "patientx";
    public override string LicenseUrl => "https://github.com/patientx/ComfyUI-Zluda/blob/master/LICENSE";
    public override string Blurb =>
        "Windows-only version of ComfyUI which uses ZLUDA to get better performance with AMD GPUs.";
    public override string Disclaimer =>
        "Prerequisite install may require admin privileges and a reboot. "
        + "AMD GPUs under the RX 6800 may require additional manual setup.";
    public override string LaunchCommand => Path.Combine("zluda", "zluda.exe");
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Zluda];

    public override TorchIndex GetRecommendedTorchVersion() => TorchIndex.Zluda;

    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_11_13;

    public override bool IsCompatible => HardwareHelper.PreferDirectMLOrZluda();

    public override bool ShouldIgnoreReleases => true;

    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.HipSdk]);

    public override bool InstallRequiresAdmin => true;
    public override string AdminRequiredReason =>
        "HIP SDK installation and (if applicable) ROCmLibs patching requires admin privileges for accessing the HIP SDK files in the Program Files directory.";

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!PrerequisiteHelper.IsHipSdkInstalled) // for updates
        {
            progress?.Report(new ProgressReport(-1, "Installing HIP SDK 6.2", isIndeterminate: true));
            await PrerequisiteHelper
                .InstallPackageRequirements(this, options.PythonOptions.PythonVersion, progress)
                .ConfigureAwait(false);
        }

        // download & setup hip sdk extension if not already done
        var hipPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "AMD",
            "ROCm",
            "6.2"
        );
        var hipblasltPath = new DirectoryPath(hipPath, "hipblaslt");
        var otherHipPath = new DirectoryPath(hipPath, "include", "hipblaslt");

        if (!hipblasltPath.Exists || !otherHipPath.Exists)
        {
            var hipSdkExtensionPath = new FilePath(
                SettingsManager.LibraryDir,
                "Assets",
                "hip-sdk-extension.7z"
            );
            await DownloadService
                .DownloadToFileAsync(
                    HipSdkExtensionDownloadUrl,
                    hipSdkExtensionPath,
                    progress,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            await ArchiveHelper.Extract7Z(hipSdkExtensionPath, hipPath, progress).ConfigureAwait(false);
        }

        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        var pipArgs = new PipInstallArgs()
            .AddArg("--force-reinstall")
            .WithTorch("==2.7.0")
            .WithTorchVision("==0.22.0")
            .WithTorchAudio("==2.7.0")
            .WithTorchExtraIndex("cu118");

        var requirements = new FilePath(installLocation, "requirements.txt");
        pipArgs = pipArgs.WithParsedFromRequirementsTxt(
            await requirements.ReadAllTextAsync(cancellationToken).ConfigureAwait(false),
            excludePattern: "torch$|numpy"
        );

        pipArgs = pipArgs.AddArg("numpy==1.26.0");

        if (installedPackage.PipOverrides != null)
        {
            pipArgs = pipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        progress?.Report(
            new ProgressReport(-1f, "Installing Package Requirements...", isIndeterminate: true)
        );
        await venvRunner.PipInstall(pipArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1, "Installed Package Requirements", isIndeterminate: false));

        progress?.Report(new ProgressReport(-1f, "Setting up ZLUDA...", isIndeterminate: true));

        // patch zluda
        var zludaPatchPath = new FilePath(installLocation, "zluda.zip");
        var zludaExtractPath = new DirectoryPath(installLocation, "zluda");
        if (zludaExtractPath.Exists)
        {
            await zludaExtractPath.DeleteAsync(true).ConfigureAwait(false);
        }
        zludaExtractPath.Create();

        await downloadService
            .DownloadToFileAsync(
                ZludaPatchDownloadUrl,
                zludaPatchPath,
                progress,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        await ArchiveHelper.Extract(zludaPatchPath, zludaExtractPath, progress).ConfigureAwait(false);
        await zludaPatchPath.DeleteAsync(cancellationToken).ConfigureAwait(false);

        // copy some stuff into the venv
        var cublasSourcePath = new FilePath(installLocation, "zluda", "cublas.dll");
        var cusparseSourcePath = new FilePath(installLocation, "zluda", "cusparse.dll");
        var nvrtc112SourcePath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "nvrtc64_112_0.dll"
        );
        var nvrtcSourcePath = new FilePath(installLocation, "zluda", "nvrtc.dll");
        var cudnnSourcePath = new FilePath(installLocation, "zluda", "cudnn.dll");
        var cufftSourcePath = new FilePath(installLocation, "zluda", "cufft.dll");
        var cufftwSourcePath = new FilePath(installLocation, "zluda", "cufftw.dll");
        var zludaPySourcePath = new FilePath(installLocation, "comfy", "customzluda", "zluda.py");

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
        var nvrtc112DestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "nvrtc_cuda.dll"
        );
        var nvrtcDestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "nvrtc64_112_0.dll"
        );
        var cudnnDestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "cudnn64_9.dll"
        );
        var cufftDestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "cufft64_10.dll"
        );
        var cufftwDestPath = new FilePath(
            venvRunner.RootPath,
            "Lib",
            "site-packages",
            "torch",
            "lib",
            "cufftw64_10.dll"
        );
        var zludaPyDestPath = new FilePath(installLocation, "comfy", "zluda.py");

        await cublasSourcePath.CopyToAsync(cublasDestPath, true).ConfigureAwait(false);
        await cusparseSourcePath.CopyToAsync(cusparseDestPath, true).ConfigureAwait(false);
        await nvrtc112SourcePath.CopyToAsync(nvrtc112DestPath, true).ConfigureAwait(false);
        await nvrtcSourcePath.CopyToAsync(nvrtcDestPath, true).ConfigureAwait(false);
        await cudnnSourcePath.CopyToAsync(cudnnDestPath, true).ConfigureAwait(false);
        await cufftSourcePath.CopyToAsync(cufftDestPath, true).ConfigureAwait(false);
        await cufftwSourcePath.CopyToAsync(cufftwDestPath, true).ConfigureAwait(false);
        await zludaPySourcePath.CopyToAsync(zludaPyDestPath, true).ConfigureAwait(false);

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
        if (!PrerequisiteHelper.IsHipSdkInstalled)
        {
            throw new MissingPrerequisiteException(
                "HIP SDK",
                "Your package has not yet been upgraded to use HIP SDK 6.2. To continue, please update this package or select \"Change Version\" from the 3-dots menu to have it upgraded automatically for you"
            );
        }
        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);
        var portableGitBin = new DirectoryPath(PrerequisiteHelper.GitBinPath);
        var hipPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "AMD",
            "ROCm",
            "6.2"
        );
        var hipBinPath = Path.Combine(hipPath, "bin");
        var envVars = new Dictionary<string, string>
        {
            ["ZLUDA_COMGR_LOG_LEVEL"] = "1",
            ["HIP_PATH"] = hipPath,
            ["HIP_PATH_62"] = hipPath,
            ["GIT"] = portableGitBin.JoinFile("git.exe"),
        };
        envVars.Update(settingsManager.Settings.EnvironmentVariables);

        if (envVars.TryGetValue("PATH", out var pathValue))
        {
            envVars["PATH"] = Compat.GetEnvPathWithExtensions(hipBinPath, portableGitBin, pathValue);
        }
        else
        {
            envVars["PATH"] = Compat.GetEnvPathWithExtensions(hipBinPath, portableGitBin);
        }

        var zludaPath = Path.Combine(installLocation, LaunchCommand);
        ProcessArgs args = ["--", VenvRunner.PythonPath.ToString(), "main.py", .. options.Arguments];
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
