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
            progress?.Report(new ProgressReport(-1, "Installing HIP SDK 6.4", isIndeterminate: true));
            await PrerequisiteHelper
                .InstallPackageRequirements(this, options.PythonOptions.PythonVersion, progress)
                .ConfigureAwait(false);
        }

        if (options.IsUpdate)
        {
            return;
        }

        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        var installNBatPath = new FilePath(installLocation, "install-n.bat");
        var newInstallBatPath = new FilePath(installLocation, "install-sm.bat");

        var installNText = await installNBatPath.ReadAllTextAsync(cancellationToken).ConfigureAwait(false);
        var installNLines = installNText.Split(Environment.NewLine);
        var cutoffIndex = Array.FindIndex(installNLines, line => line.Contains("Installation is completed"));

        IEnumerable<string> filtered = installNLines;
        if (cutoffIndex >= 0)
        {
            filtered = installNLines.Take(cutoffIndex);
        }

        newInstallBatPath.Create();
        await newInstallBatPath
            .WriteAllTextAsync(string.Join(Environment.NewLine, filtered), cancellationToken)
            .ConfigureAwait(false);

        var installProcess = ProcessRunner.StartAnsiProcess(
            newInstallBatPath,
            [],
            installLocation,
            onConsoleOutput,
            GetEnvVars(true)
        );
        await installProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(new ProgressReport(1, "Installed Successfully", isIndeterminate: false));
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
                "Your package has not yet been upgraded to use HIP SDK 6.4. To continue, please update this package or select \"Change Version\" from the 3-dots menu to have it upgraded automatically for you"
            );
        }
        await SetupVenv(installLocation, pythonVersion: PyVersion.Parse(installedPackage.PythonVersion))
            .ConfigureAwait(false);

        var zludaPath = Path.Combine(installLocation, LaunchCommand);
        ProcessArgs args = ["--", VenvRunner.PythonPath.ToString(), "main.py", .. options.Arguments];
        zludaProcess = ProcessRunner.StartAnsiProcess(
            zludaPath,
            args,
            installLocation,
            HandleConsoleOutput,
            GetEnvVars(false)
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

    private Dictionary<string, string> GetEnvVars(bool isInstall)
    {
        var portableGitBin = new DirectoryPath(PrerequisiteHelper.GitBinPath);
        var hipPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "AMD",
            "ROCm",
            "6.4"
        );
        var hipBinPath = Path.Combine(hipPath, "bin");
        var envVars = new Dictionary<string, string>
        {
            ["ZLUDA_COMGR_LOG_LEVEL"] = "1",
            ["HIP_PATH"] = hipPath,
            ["HIP_PATH_64"] = hipPath,
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

        if (isInstall)
            return envVars;

        envVars["FLASH_ATTENTION_TRITON_AMD_ENABLE"] = "TRUE";
        envVars["MIOPEN_FIND_MODE"] = "2";
        envVars["MIOPEN_LOG_LEVEL"] = "3";

        return envVars;
    }
}
