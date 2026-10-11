using System.Collections.Immutable;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, ForgeNeo>(Duplicate = DuplicateStrategy.Append)]
public class ForgeNeo(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService,
    IRocmPackageHelper rocmPackageHelper
)
    : ForgeClassic(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    internal const string LinuxRocmIndexUrl = "https://download.pytorch.org/whl/rocm7.14";

    // Linux uses the upstream PyTorch ROCm index rather than AMD repo wheels. Kept inline because it
    // collapses into the helper once its OS-agnostic path lands.
    private static readonly RocmPackageProfile LinuxRocmProfile = new()
    {
        ExtraEnvironmentFactory = _ => new Dictionary<string, string>
        {
            ["TORCH_INDEX_URL"] = LinuxRocmIndexUrl,
            ["TORCH_COMMAND"] = $"pip install --index-url {LinuxRocmIndexUrl} torch torchvision",
        },
    };

    /// <summary>
    /// Windows installs use AMD repo wheels; Linux uses the upstream PyTorch ROCm index.
    /// </summary>
    private RocmPackageProfile ActiveRocmProfile =>
        Compat.IsWindows ? ForgeNeoWindowsRocmProfile.Default : LinuxRocmProfile;

    public override string Name => "forge-neo";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI Forge - Neo";
    public override string MainBranch => "neo";
    public override PackageType PackageType => PackageType.SdInference;

    // CUDA for NVIDIA; ROCm for AMD.
    public override IEnumerable<TorchIndex> AvailableTorchIndices => [TorchIndex.Cuda, TorchIndex.Rocm];

    public override bool IsCompatible => HardwareHelper.HasNvidiaGpu() || HasRocmSupport();

    /// <summary>
    /// Uses the shared ROCm helper for ROCm eligibility checks so Forge Neo does not maintain its own
    /// support matrix. The helper resolves compatibility for both Windows and Linux.
    /// </summary>
    private bool HasRocmSupport()
    {
        return rocmPackageHelper.GetCompatibility().IsCompatible;
    }

    public override TorchIndex GetRecommendedTorchVersion()
    {
        var preferNvidia = SettingsManager.Settings.PreferredGpu?.IsNvidia ?? HardwareHelper.HasNvidiaGpu();
        if (preferNvidia)
        {
            return TorchIndex.Cuda;
        }

        if (HasRocmSupport())
        {
            return TorchIndex.Rocm;
        }

        return base.GetRecommendedTorchVersion();
    }

    public override List<LaunchOptionDefinition> LaunchOptions
    {
        get
        {
            var options = new List<LaunchOptionDefinition>(base.LaunchOptions);
            var insertIndex = Math.Max(0, options.Count - 1);
            options.Insert(
                insertIndex,
                new LaunchOptionDefinition
                {
                    Name = "Bitsandbytes NF4",
                    Type = LaunchOptionType.Bool,
                    Description = "Install bitsandbytes for low-bits (NF4) inference",
                    Options = ["--bnb"],
                }
            );

            ForgeNeoWindowsRocmProfile.Default.ApplyWindowsRocmLaunchDefaults(options, rocmPackageHelper);

            return options;
        }
    }

    protected override async Task PrepareRocmInstallAsync(
        IPyVenvRunner venvRunner,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        Action<ProcessOutput>? onConsoleOutput,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken
    )
    {
        var torchIndex = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();
        if (torchIndex != TorchIndex.Rocm)
        {
            return;
        }

        if (!HasRocmSupport())
        {
            throw new InvalidOperationException(
                rocmPackageHelper.GetCompatibility().FailureReason
                    ?? "The current machine is not compatible with the selected ROCm torch index."
            );
        }

        var profile = ActiveRocmProfile;

        progress?.Report(new ProgressReport(-1f, "Preparing ROCm environment...", isIndeterminate: true));

        // Apply the ROCm launch environment (including the TORCH_COMMAND/TORCH_INDEX_URL override) so both
        // the torch install and the subsequent upstream launch.py see it.
        venvRunner.UpdateEnvironmentVariables(env =>
            env.SetItems(rocmPackageHelper.BuildLaunchEnvironment(profile))
        );

        if (Compat.IsWindows)
        {
            // Install the AMD multi-arch torch build before launch.py runs, so its is_installed("torch")
            // gate skips the default NVIDIA CUDA torch install.
            await rocmPackageHelper
                .InstallWindowsNativeTorchAsync(
                    venvRunner,
                    installedPackage,
                    profile,
                    progress,
                    onConsoleOutput,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        // Linux: install from the upstream PyTorch ROCm index. An exclusive --index-url (rather than
        // --extra-index-url) keeps pip from quietly resolving the CPU build from PyPI.
        var torchArgs = new PipInstallArgs()
            .AddKeyedArgs("--index-url", ["--index-url", LinuxRocmIndexUrl])
            .AddArg("torch")
            .AddArg("torchvision");

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

        await VerifyLinuxRocmTorchInstallAsync(venvRunner, cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyLinuxRocmTorchInstallAsync(
        IPyVenvRunner venvRunner,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            throw new InvalidOperationException("torch was not installed after Linux ROCm setup.");
        }

        if (
            string.IsNullOrWhiteSpace(torchInfo.Version)
            || !torchInfo.Version.Contains("rocm", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                $"Installed torch is not a ROCm build (detected version: {torchInfo.Version})."
            );
        }
    }

    protected override ImmutableDictionary<string, string> GetEnvVars(
        ImmutableDictionary<string, string> env,
        InstalledPackage installedPackage
    )
    {
        env = base.GetEnvVars(env, installedPackage);

        var selectedTorchIndex = installedPackage.PreferredTorchIndex ?? GetRecommendedTorchVersion();
        if (selectedTorchIndex != TorchIndex.Rocm || !HasRocmSupport())
        {
            return env;
        }

        return env.SetItems(rocmPackageHelper.BuildLaunchEnvironment(ActiveRocmProfile));
    }

    protected override IReadOnlyList<string> GetLaunchNoticeLines(InstalledPackage installedPackage)
    {
        var selectedTorchIndex = installedPackage.PreferredTorchIndex ?? GetRecommendedTorchVersion();
        // The helper returns an empty list on non-Windows platforms.
        return rocmPackageHelper.GetWindowsLaunchNoticeLines(selectedTorchIndex);
    }

    public override string Blurb =>
        "Neo mainly serves as an continuation for the \"latest\" version of Forge. Additionally, this fork is focused on optimization and usability, with the main goal of being the lightest WebUI without any bloatwares.";
}
