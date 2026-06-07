using System.Collections.Immutable;
using Injectio.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Packages;

[RegisterSingleton<BasePackage, Reforge>(Duplicate = DuplicateStrategy.Append)]
public class Reforge(
    IGithubApiCache githubApi,
    ISettingsManager settingsManager,
    IDownloadService downloadService,
    IPrerequisiteHelper prerequisiteHelper,
    IPyInstallationManager pyInstallationManager,
    IPipWheelService pipWheelService,
    IRocmPackageHelper rocmPackageHelper
)
    : SDWebForge(
        githubApi,
        settingsManager,
        downloadService,
        prerequisiteHelper,
        pyInstallationManager,
        pipWheelService
    )
{
    public override string Name => "reforge";
    public override string Author => "Panchovix";
    public override string RepositoryName => "stable-diffusion-webui-reForge";
    public override string DisplayName { get; set; } = "Stable Diffusion WebUI reForge";
    public override string Blurb =>
        "Stable Diffusion WebUI reForge is a platform on top of Stable Diffusion WebUI (based on Gradio) to make development easier, optimize resource management, speed up inference, and study experimental features.";
    public override string LicenseUrl =>
        "https://github.com/Panchovix/stable-diffusion-webui-reForge/blob/main/LICENSE.txt";
    public override Uri PreviewImageUri => new("https://cdn.lykos.ai/sm/packages/reforge/preview.webp");
    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Recommended;
    public override bool OfferInOneClickInstaller => true;
    public override PackageType PackageType => PackageType.SdInference;
    public override PyVersion RecommendedPythonVersion => Python.PyInstallationManager.Python_3_12_10;

    private const string StableDiffusionRepoOverride =
        "https://github.com/joypaul162/Stability-AI-stablediffusion.git";

    public override List<LaunchOptionDefinition> LaunchOptions
    {
        get
        {
            var baseLaunchOptions = new List<LaunchOptionDefinition>(base.LaunchOptions);
            var extrasIndex = baseLaunchOptions.FindIndex(x => x.Name == LaunchOptionDefinition.Extras.Name);

            // Adjust inherited launch defaults for the Windows ROCm path before inserting reForge-specific options.
            // Makes the reForge-specific attention options visible in the UI and leaves them unset by default
            // for non-Windows-ROCm installs so reForge can keep using its normal internal attention selection.
            ReforgeWindowsRocmProfile.Default.ApplyWindowsRocmLaunchDefaults(
                baseLaunchOptions,
                rocmPackageHelper
            );

            baseLaunchOptions.Insert(
                extrasIndex >= 0 ? extrasIndex : baseLaunchOptions.Count,
                new LaunchOptionDefinition
                {
                    Name = "Cross Attention Method",
                    Type = LaunchOptionType.Bool,
                    InitialValue = ReforgeWindowsRocmProfile.Default.GetPreferredCrossAttentionArgument(
                        rocmPackageHelper
                    ),
                    Options = ["--attention-split", "--attention-quad", "--attention-pytorch"],
                }
            );

            return baseLaunchOptions;
        }
    }

    // Prefer ROCm on Linux AMD systems and use the helper-managed Windows ROCm install/launch flow when supported.
    public override TorchIndex GetRecommendedTorchVersion()
    {
        var preferRocm =
            (Compat.IsLinux && (SettingsManager.Settings.PreferredGpu?.IsAmd ?? HardwareHelper.PreferRocm()))
            || rocmPackageHelper.GetCompatibility().IsCompatible;

        if (AvailableTorchIndices.Contains(TorchIndex.Rocm) && preferRocm)
        {
            return TorchIndex.Rocm;
        }

        return base.GetRecommendedTorchVersion();
    }

    public override async Task InstallPackage(
        string installLocation,
        InstalledPackage installedPackage,
        InstallPackageOptions options,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var torchIndex = options.PythonOptions.TorchIndex ?? GetRecommendedTorchVersion();

        if (!rocmPackageHelper.ShouldApplyWindowsLaunchEnvironment(torchIndex))
        {
            await base.InstallPackage(
                    installLocation,
                    installedPackage,
                    options,
                    progress,
                    onConsoleOutput,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return;
        }

        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        await using var venvRunner = await SetupVenvPure(
                installLocation,
                pythonVersion: options.PythonOptions.PythonVersion
            )
            .ConfigureAwait(false);

        var profile = ReforgeWindowsRocmProfile.CreateProfile(GetRequirementsPaths(installLocation));
        var config = rocmPackageHelper.BuildWindowsNativeInstallConfig(profile);

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

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }

    protected override ImmutableDictionary<string, string> GetEnvVars(
        ImmutableDictionary<string, string> env,
        InstalledPackage installedPackage
    )
    {
        var selectedTorchIndex = installedPackage.PreferredTorchIndex ?? GetRecommendedTorchVersion();

        env = base.GetEnvVars(env, installedPackage);
        env = env.SetItem("STABLE_DIFFUSION_REPO", StableDiffusionRepoOverride);

        if (!rocmPackageHelper.ShouldApplyWindowsLaunchEnvironment(selectedTorchIndex))
        {
            return env;
        }

        return env.SetItems(rocmPackageHelper.BuildLaunchEnvironment(ReforgeWindowsRocmProfile.Default));
    }

    protected override IReadOnlyList<string> GetLaunchNoticeLines(InstalledPackage installedPackage)
    {
        var selectedTorchIndex = installedPackage.PreferredTorchIndex ?? GetRecommendedTorchVersion();
        return rocmPackageHelper.GetWindowsLaunchNoticeLines(selectedTorchIndex);
    }
}
