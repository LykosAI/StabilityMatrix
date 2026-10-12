using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Fizgig>(Duplicate = DuplicateStrategy.Append)]
public partial class Fizgig(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService
)
    : BaseGitPackage(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    public override string Name => "Fizgig";
    public override string DisplayName { get; set; } = "Fizgig";
    public override string Author => "shootthesound";

    public override string Blurb =>
        "LoRA training studio for Flux 2 Klein 9B, Krea 2, MiniMax H3 and Qwen Image 2.1. Train, profile, repair and extract";

    // Shown in the install browser before the user commits to installing.
    public override string Disclaimer =>
        Compat.IsWindows
            ? "Visual Studio Build Tools for C++ Desktop Development will be installed system-wide if not already present (may require admin privileges). "
                + "They are shared with other software and remain installed after Fizgig is uninstalled."
            : string.Empty;

    public override string LicenseType => "Apache-2.0";
    public override string LicenseUrl => "https://github.com/shootthesound/Fizgig/blob/master/LICENSE";

    // NOT launch.pyw: that launcher re-spawns itself under venv/Scripts/pythonw.exe and exits,
    // which would drop the process we track (no console output, no working Stop button) and
    // leave the GUI orphaned. lora_trainer_gui.py has a standalone main() and is what upstream's
    // run_fizgig.sh invokes directly.
    public override string LaunchCommand => "lora_trainer_gui.py";

    public override Uri PreviewImageUri =>
        new("https://github.com/shootthesound/Fizgig/blob/master/icon.png?raw=true");

    public override string MainBranch => "master";
    public override PackageType PackageType => PackageType.SdTraining;
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Advanced;
    public override bool OfferInOneClickInstaller => false;
    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu();
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda];

    public override TorchIndex GetRecommendedTorchVersion() => TorchIndex.Cuda;

    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;

    // Tkinter for the GUI itself; VcBuildTools for triton / torch.compile's inductor backend,
    // which the Compile Blocks speedup needs on Windows.
    public override IEnumerable<PackagePrerequisite> Prerequisites =>
        base.Prerequisites.Concat([PackagePrerequisite.Tkinter, PackagePrerequisite.VcBuildTools]);

    public override List<LaunchOptionDefinition> LaunchOptions => [LaunchOptionDefinition.Extras];

    // Trained LoRAs, not images.
    public override string OutputFolderName => string.Empty;
    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders => null;

    /// <remarks>
    /// None only, like the other trainers. output_loras also receives sample images and multi-GB
    /// resume-state folders, so linking it into the shared Lora folder would leak those into other
    /// packages. Users who want trained LoRAs there can point Fizgig's Output Directory at it.
    /// models/ can't be shared either: Fizgig flattens every weight it downloads into that one
    /// directory, which doesn't map onto the per-type shared folders.
    /// </remarks>
    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.None;

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods => [SharedFolderMethod.None];

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

        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        // hqq ships as an sdist whose setup.py kicks off a CUDA kernel build during egg_info
        // unless DISABLE_CUDA is set. Fizgig only uses its pure-PyTorch path, and its own
        // requirements.txt warns never to install that line without this.
        venvRunner.UpdateEnvironmentVariables(env => env.SetItem("DISABLE_CUDA", "1"));

        // Mirrors upstream's uv_install_deps.py: torch goes in first from the CUDA index, then
        // the rest of requirements.txt without that index. The torch pins stay in the second
        // step so nothing can swap the CUDA build for a PyPI one.
        var (extraIndexUrl, torchSpecs) = await ParseRequirementsAsync(
                Path.Combine(installLocation, "requirements.txt"),
                cancellationToken
            )
            .ConfigureAwait(false);

        var config = new PipInstallConfig
        {
            RequirementsFilePaths = ["requirements.txt"],
            RequirementsExcludePattern = "--extra-index-url.*",
            PrePipInstallArgs =
                extraIndexUrl is not null && torchSpecs.Count > 0
                    ? [.. torchSpecs, "--extra-index-url", extraIndexUrl]
                    : [],
            SkipTorchInstall = true,
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
    }

    private static readonly string[] TorchEcosystem = ["torch", "torchvision", "torchaudio"];

    /// <summary>
    /// Port of _parse_requirements in upstream's uv_install_deps.py: returns the single
    /// --extra-index-url and the torch-ecosystem requirement lines.
    /// </summary>
    private static async Task<(string? ExtraIndexUrl, List<string> TorchSpecs)> ParseRequirementsAsync(
        string requirementsPath,
        CancellationToken cancellationToken
    )
    {
        string? extraIndexUrl = null;
        var torchSpecs = new List<string>();

        var lines = await File.ReadAllLinesAsync(requirementsPath, cancellationToken).ConfigureAwait(false);
        foreach (var raw in lines)
        {
            var code = raw.Split('#', 2)[0].Trim();
            if (code.StartsWith("--extra-index-url", StringComparison.Ordinal))
            {
                if (extraIndexUrl is not null)
                {
                    throw new InvalidOperationException(
                        "Fizgig's requirements.txt declares more than one --extra-index-url"
                    );
                }

                var parts = code.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    extraIndexUrl = parts[1];
                }
                continue;
            }

            if (code.Length > 0)
            {
                var pkg = RequirementNameEndRegex().Split(code, 2)[0];
                if (TorchEcosystem.Contains(pkg))
                {
                    torchSpecs.Add(code);
                }
            }
        }

        return (extraIndexUrl, torchSpecs);
    }

    [GeneratedRegex(@"[=<>!~\s\[;]")]
    private static partial Regex RequirementNameEndRegex();

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

        // Desktop Tkinter app - there is no local URL to wait for, so startup is complete
        // as soon as the process is up.
        VenvRunner.RunDetached(
            [Path.Combine(installLocation, options.Command ?? LaunchCommand), .. options.Arguments],
            onConsoleOutput,
            OnExit
        );
    }
}
