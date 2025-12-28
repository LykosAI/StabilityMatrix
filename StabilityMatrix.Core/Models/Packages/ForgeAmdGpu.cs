using System.Collections.Immutable;
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

[RegisterSingleton<BasePackage, ForgeAmdGpu>(Duplicate = DuplicateStrategy.Append)]
public class ForgeAmdGpu(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager
) : SDWebForge(githubApi, settingsManager, downloadService, prerequisiteHelper, pyInstallationManager)
{
    public override string Name => "stable-diffusion-webui-amdgpu-forge";
    public override string DisplayName => "Stable Diffusion WebUI AMDGPU Forge";
    public override string Author => "lshqqytiger";
    public override string RepositoryName => "stable-diffusion-webui-amdgpu-forge";
    public override string Blurb => "A fork of Stable Diffusion WebUI Forge with support for AMD GPUs";

    public override string LicenseUrl =>
        "https://github.com/lshqqytiger/stable-diffusion-webui-amdgpu-forge/blob/main/LICENSE.txt";

    public override string Disclaimer =>
        "Prerequisite install may require admin privileges and a reboot. "
        + "AMD GPUs under the RX 6800 may require additional manual setup.";

    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Zluda];

    public override TorchIndex GetRecommendedTorchVersion() => TorchIndex.Zluda;

    public override bool IsCompatible => HardwareHelper.PreferDirectMLOrZluda();

    public override PackageType PackageType => PackageType.SdInference;

    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.HipSdk]);

    public override List<LaunchOptionDefinition> LaunchOptions =>
        base
            .LaunchOptions.Concat(
                [
                    new LaunchOptionDefinition
                    {
                        Name = "Use ZLUDA",
                        Description = "Use ZLUDA for CUDA acceleration on AMD GPUs",
                        Type = LaunchOptionType.Bool,
                        InitialValue = HardwareHelper.PreferDirectMLOrZluda(),
                        Options = ["--use-zluda"],
                    },
                    new LaunchOptionDefinition
                    {
                        Name = "Use DirectML",
                        Description = "Use DirectML for DirectML acceleration on compatible GPUs",
                        Type = LaunchOptionType.Bool,
                        InitialValue = false,
                        Options = ["--use-directml"],
                    },
                ]
            )
            .ToList();

    public override bool InstallRequiresAdmin => true;

    public override string AdminRequiredReason =>
        "HIP SDK installation and (if applicable) ROCmLibs patching requires admin "
        + "privileges for accessing the HIP SDK files in the Program Files directory.";

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

        progress?.Report(new ProgressReport(-1, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);
        progress?.Report(new ProgressReport(1, "Install finished", isIndeterminate: false));
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

        VenvRunner.UpdateEnvironmentVariables(env => envVars.ToImmutableDictionary());

        VenvRunner.RunDetached(
            [
                Path.Combine(installLocation, options.Command ?? LaunchCommand),
                .. options.Arguments,
                .. ExtraLaunchArguments,
            ],
            HandleConsoleOutput,
            OnExit
        );
        return;

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
    }
}
