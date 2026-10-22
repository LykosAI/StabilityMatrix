using System.Text.Json;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Rocm;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Services.Rocm;

/// <summary>
/// Provides the shared ROCm helper surface area used by ROCm-capable packages.
/// </summary>
[RegisterSingleton<IRocmPackageHelper, RocmPackageHelper>]
public class RocmPackageHelper : IRocmPackageHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly StringComparer EnvComparer = StringComparer.OrdinalIgnoreCase;

    private const string RocmSdkDevelPackageName = "rocm-sdk-devel";

    // The ROCm torch install only ever needs to move the torch trio. Bundling or upgrading anything
    // wider lets the resolver re-evaluate transitive dependencies against AMD's feed.
    private static readonly string[] RocmTorchPackageNames = ["torch", "torchvision", "torchaudio"];
    private static readonly string[] RocmTorchUpgradePackageArgs = BuildRepeatablePackageArgs(
        "--upgrade-package",
        RocmTorchPackageNames
    );
    private static readonly string[] RocmTorchReinstallPackageArgs = BuildRepeatablePackageArgs(
        "--reinstall-package",
        RocmTorchPackageNames
    );
    private static readonly string[] LaunchNoticeLines =
    [
        "Stability Matrix ROCm Notice: AMD ROCm support is experimental. Please report any issues to Stability Matrix first so it can be determined whether the issue is package-specific.",
        "Because this setup may not be officially supported by package developers, only contact upstream support for issues clearly caused by the package itself.",
    ];

    private readonly ISettingsManager settingsManager;

    // Hardware enumeration is expensive and GPU state does not change during a session.
    // Cache the resolved machine state once per singleton lifetime.
    private readonly Lazy<RocmMachineState> machineState;

    public RocmPackageHelper(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
        machineState = new Lazy<RocmMachineState>(ResolveMachineState);
    }

    /// <summary>
    /// Evaluates the current machine state and returns the resolved ROCm compatibility result.
    /// </summary>
    public RocmCompatibilityResult GetCompatibility()
    {
        return BuildCompatibilityResult();
    }

    /// <summary>
    /// Resolves launch-time ROCm runtime details from the current machine state.
    /// This is used to build helper-managed environment variables for package launch.
    /// </summary>
    private RocmRuntimeContext ResolveRuntimeContext()
    {
        var state = machineState.Value;
        if (!state.IsCompatible)
        {
            return new RocmRuntimeContext
            {
                IsSupported = false,
                FailureReason = state.FailureReason,
                SelectedGpu = state.SelectedGpu,
                RuntimeGfxArch = state.RuntimeGfxArch,
            };
        }

        return new RocmRuntimeContext
        {
            IsSupported = true,
            SelectedGpu = state.SelectedGpu,
            RuntimeGfxArch = state.RuntimeGfxArch,
            AmdDeviceCount = state.AmdDeviceCount,
            DeviceIndex = state.DeviceIndex,
        };
    }

    /// <summary>
    /// Builds the final launch environment for a ROCm-capable package by combining helper defaults,
    /// package-specific environment values, and optional user overrides.
    /// </summary>
    public IReadOnlyDictionary<string, string> BuildLaunchEnvironment(RocmPackageProfile profile)
    {
        var runtimeContext = ResolveRuntimeContext();

        if (!runtimeContext.IsSupported)
            return new Dictionary<string, string>();

        var helperEnvironment = BuildHelperLaunchEnvironment(runtimeContext, profile);
        var packageEnvironment =
            profile.ExtraEnvironmentFactory?.Invoke(runtimeContext) ?? new Dictionary<string, string>();

        var mergedEnvironment = MergeLaunchEnvironment(
            helperEnvironment,
            packageEnvironment,
            profile.EnvironmentOptions
        );

        return mergedEnvironment;
    }

    private IReadOnlyList<string> GetLaunchNoticeLines()
    {
        return LaunchNoticeLines;
    }

    /// <inheritdoc />
    public bool ShouldApplyLaunchEnvironment(TorchIndex selectedTorchIndex)
    {
        if (selectedTorchIndex != TorchIndex.Rocm)
            return false;
        return GetCompatibility().IsCompatible;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetLaunchNoticeLines(TorchIndex selectedTorchIndex)
    {
        return ShouldApplyLaunchEnvironment(selectedTorchIndex) ? GetLaunchNoticeLines() : [];
    }

    /// <summary>
    /// Ensures <c>rocm-sdk-devel</c> is installed from the resolved ROCm index, on the same ROCm line as
    /// the installed torch build. Nightly builds match on their build date token, stable builds on the
    /// ROCm version in torch's local label, and only when neither matches is the newest build used.
    /// </summary>
    public async Task EnsureRocmSdkDevelAsync(
        IPyVenvRunner venvRunner,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var state = machineState.Value;
        var target = RocmSupport.ResolveTarget(state.RuntimeGfxArch);

        if (!target.SupportsSdkDevel)
        {
            Logger.Info(
                "The {IndexKind} index does not publish {Package}; skipping the SDK devel step.",
                target.IndexKind,
                RocmSdkDevelPackageName
            );
            return;
        }

        var indexUrl = target.IndexUrl;

        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            throw new InvalidOperationException(
                "torch is not installed in this environment. Install the ROCm torch build first."
            );
        }

        if (!IsUsableRocmTorchBuild(torchInfo.Version, null))
        {
            throw new InvalidOperationException(
                $"Installed torch is not a usable ROCm build (detected version: {torchInfo.Version})."
            );
        }

        var nightlyBuildDateToken = TryGetNightlyBuildDateToken(torchInfo.Version);
        var rocmVersion = TryGetRocmVersionLabel(torchInfo.Version);
        var installedRocmSdkDevel = await venvRunner.PipShow(RocmSdkDevelPackageName).ConfigureAwait(false);

        // Already on the right line: a nightly build with the matching date token, or a stable build
        // matching the ROCm version the torch wheel was compiled against.
        if (
            (
                !string.IsNullOrWhiteSpace(nightlyBuildDateToken)
                && HasNightlyBuildDateToken(installedRocmSdkDevel?.Version, nightlyBuildDateToken)
            ) || IsRocmSdkDevelMatch(installedRocmSdkDevel?.Version, rocmVersion)
        )
        {
            return;
        }

        var indexResult = await venvRunner.PipIndex(RocmSdkDevelPackageName, indexUrl).ConfigureAwait(false);

        var selection = SelectSdkDevelVersion(
            indexResult?.AvailableVersions,
            rocmVersion,
            nightlyBuildDateToken
        );

        if (selection is not { } chosen)
        {
            throw new InvalidOperationException(
                $"No {RocmSdkDevelPackageName} builds were found on the ROCm index."
            );
        }

        var versionToInstall = chosen.Version;
        var matchingVersion = chosen.IsExactMatch ? versionToInstall : null;

        if (!string.IsNullOrWhiteSpace(matchingVersion))
        {
            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Installing {RocmSdkDevelPackageName} {matchingVersion} for ROCm...",
                    isIndeterminate: true
                )
            );
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(rocmVersion))
            {
                Logger.Warn(
                    "No {Package} build matches the ROCm {RocmVersion} line of the installed torch "
                        + "({TorchVersion}); installing {Version} instead. Builds against the development "
                        + "SDK may then not load in this environment.",
                    RocmSdkDevelPackageName,
                    rocmVersion,
                    torchInfo.Version,
                    versionToInstall
                );
            }

            progress?.Report(
                new ProgressReport(
                    -1f,
                    $"Falling back to latest available {RocmSdkDevelPackageName} build {versionToInstall} for ROCm...",
                    isIndeterminate: true
                )
            );
        }

        await venvRunner
            .PipInstall(
                new PipInstallArgs()
                    .AddArg("--upgrade")
                    .AddKeyedArgs("--index-url", ["--index-url", indexUrl])
                    .AddArg($"{RocmSdkDevelPackageName}=={versionToInstall}"),
                onConsoleOutput
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the standard pip install config for helper-managed ROCm package installs.
    /// </summary>
    public PipInstallConfig BuildRocmInstallConfig(RocmPackageProfile profile)
    {
        return profile.InstallConfig with { SkipTorchInstall = true };
    }

    /// <summary>
    /// Installs the ROCm torch wheel set from the resolved index and verifies that the resulting torch
    /// installation reports usable ROCm metadata.
    /// </summary>
    public async Task InstallRocmTorchAsync(
        IPyVenvRunner venvRunner,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var state = machineState.Value;
        if (!state.IsCompatible)
        {
            throw new InvalidOperationException(
                state.FailureReason ?? "ROCm installation is not supported for the current machine."
            );
        }

        var target = RocmSupport.ResolveTarget(state.RuntimeGfxArch, profile.Index);

        // AMD's feeds select device code through a per-architecture extra; the upstream PyTorch feeds
        // either ship one multi-architecture wheel (e.g: 7.2) or explicitly force-installs all device extras (e.g 7.14),
        // so an index without device extras is still installable.
        if (target.SupportsDeviceExtras && string.IsNullOrWhiteSpace(target.DeviceExtra))
        {
            throw new InvalidOperationException(
                $"No ROCm device package is available for '{state.RuntimeGfxArch ?? "unknown"}' "
                    + $"on the {target.IndexKind} index."
            );
        }

        var installConfig = profile.InstallConfig;
        var indexUrl = target.IndexUrl;
        var usesNightlyIndex = target.IndexKind == RocmIndexKind.AmdNightly;

        // AMD's torchvision wheels carry no torch constraint, and the matched build may be published only
        // as a pre-release that pip skips. Resolve the newest paired torch/torchvision and pin both.
        progress?.Report(new ProgressReport(-1f, "Resolving ROCm torch...", isIndeterminate: true));

        var torchResolution = await ResolveTorchInstallAsync(
                venvRunner,
                indexUrl,
                target.DeviceExtra,
                usesNightlyIndex,
                onConsoleOutput,
                cancellationToken
            )
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(-1f, "Installing ROCm torch...", isIndeterminate: true));

        var torchArgs = ApplyRocmTorchInstallOptions(
            BuildRocmTorchArgs(
                indexUrl,
                torchResolution.TorchSpecifier,
                torchResolution.TorchvisionSpecifier
            ),
            installConfig,
            installedPackage.PipOverrides
        );

        if (torchResolution.AllowPrerelease)
        {
            torchArgs = torchArgs.AddArg("--pre");
        }

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

        if (installConfig.PostTorchInstallPipArgs.Any())
        {
            var postTorchInstallPipArgs = new PipInstallArgs([.. installConfig.PostTorchInstallPipArgs]);

            if (installedPackage.PipOverrides != null)
            {
                postTorchInstallPipArgs = postTorchInstallPipArgs.WithUserOverrides(
                    installedPackage.PipOverrides
                );
            }

            await venvRunner.PipInstall(postTorchInstallPipArgs, onConsoleOutput).ConfigureAwait(false);
        }

        await VerifyRocmTorchInstallAsync(venvRunner, onConsoleOutput, cancellationToken)
            .ConfigureAwait(false);

        // A development SDK that is already present has to follow the torch build it sits beside, or the
        // headers and libraries come from a different ROCm line than the runtime loading them.
        if (await venvRunner.PipShow(RocmSdkDevelPackageName).ConfigureAwait(false) is not null)
        {
            await EnsureRocmSdkDevelAsync(venvRunner, progress, onConsoleOutput, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies the profile's upgrade/reinstall flags and any user pip overrides to a ROCm torch install command.
    /// Scope is deliberately limited to the torch trio. A blanket <c>--upgrade</c> lets the resolver move
    /// transitive dependencies against AMD's feed, numpy foremost, where the only build may be
    /// numpy 2.5.2 (cp312+). A blanket <c>--force-reinstall</c> reinstalls the whole graph for the same reason.
    /// Packages that must follow the torch build, amd-torch-device-gfx* and triton, are exact-pinned by the
    /// wheel metadata, so they are still replaced to satisfy the resolved pin.
    /// </summary>
    internal static PipInstallArgs ApplyRocmTorchInstallOptions(
        PipInstallArgs args,
        PipInstallConfig installConfig,
        IEnumerable<PipPackageSpecifierOverride>? pipOverrides
    )
    {
        if (installConfig.UpgradePackages)
        {
            args = args.AddKeyedArgs("--upgrade-package", RocmTorchUpgradePackageArgs);
        }

        if (installConfig.ForceReinstallTorch)
        {
            args = args.AddKeyedArgs("--reinstall-package", RocmTorchReinstallPackageArgs);
        }

        if (pipOverrides != null)
        {
            args = args.WithUserOverrides(pipOverrides.ToList());
        }

        return args;
    }

    /// <summary>
    /// Expands a repeatable flag so each package gets its own <c>flag value</c> pair.
    /// </summary>
    private static string[] BuildRepeatablePackageArgs(string flag, IEnumerable<string> packages) =>
        packages.SelectMany(package => new[] { flag, package }).ToArray();

    /// <summary>
    /// Builds the pip args for the resolved torch/torchvision pair. The specs are keyed so a user
    /// override replaces the pin instead of being appended next to it (which pip cannot satisfy).
    /// </summary>
    /// <summary>
    /// Requirement form for a torch package on the resolved index: a per-architecture extra on AMD's
    /// feeds, a plain requirement on the upstream PyTorch feeds, which ship one multi-architecture wheel
    /// or all supported device libraries.
    /// </summary>
    internal static string BuildTorchSpecifier(string packageName, string? deviceExtra) =>
        string.IsNullOrWhiteSpace(deviceExtra) ? packageName : $"{packageName}[{deviceExtra}]";

    internal static PipInstallArgs BuildRocmTorchArgs(
        string indexUrl,
        string torchSpecifier,
        string torchvisionSpecifier
    )
    {
        return new PipInstallArgs()
            .AddKeyedArgs("--index-url", ["--index-url", indexUrl])
            .AddArg(new Argument(key: "torch", value: torchSpecifier))
            .AddArg(new Argument(key: "torchvision", value: torchvisionSpecifier))
            .AddArg("torchaudio");
    }

    /// <summary>
    /// Resolved torch and torchvision install specifiers, plus whether the install must allow
    /// pre-releases (the fallback path where no paired build could be resolved).
    /// </summary>
    private readonly record struct TorchInstallResolution(
        string TorchSpecifier,
        string TorchvisionSpecifier,
        bool AllowPrerelease
    );

    /// <summary>
    /// Resolves the newest torch that has a paired torchvision on the ROCm index so both can be pinned;
    /// falls back to unpinned, pre-release-allowing specifiers when no pair resolves.
    /// </summary>
    private async Task<TorchInstallResolution> ResolveTorchInstallAsync(
        IPyVenvRunner venvRunner,
        string indexUrl,
        string? deviceExtra,
        bool usesNightlyIndex,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        var torchSpecifierBase = BuildTorchSpecifier("torch", deviceExtra);
        var torchvisionSpecifierBase = BuildTorchSpecifier("torchvision", deviceExtra);

        try
        {
            // torch: finals only on the stable channel, pre/dev builds on the nightly channel.
            var torchIndex = await venvRunner
                .PipIndex("torch", indexUrl, includePrerelease: usesNightlyIndex)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // torchvision: allow pre-releases so the matched alpha (e.g. 0.29.0a0) is visible.
            var torchvisionIndex = await venvRunner
                .PipIndex("torchvision", indexUrl, includePrerelease: true)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // On the nightly channel the local label carries the build date, so pin the newest snapshot
            // that exists for both torch and the paired torchvision instead of letting pip pair builds
            // from different dates.
            if (usesNightlyIndex)
            {
                var snapshot = RocmTorchVersionResolver.SelectHighestPairBySnapshot(
                    torchIndex?.AvailableVersions,
                    torchvisionIndex?.AvailableVersions
                );

                if (snapshot is { } selectedSnapshot)
                {
                    Logger.Info(
                        "Resolved ROCm nightly snapshot torch {TorchVersion} with torchvision {TorchvisionVersion}.",
                        selectedSnapshot.Torch,
                        selectedSnapshot.Torchvision
                    );

                    return new TorchInstallResolution(
                        $"{torchSpecifierBase}=={selectedSnapshot.Torch}",
                        $"{torchvisionSpecifierBase}=={selectedSnapshot.Torchvision}",
                        AllowPrerelease: false
                    );
                }
            }

            var pair = RocmTorchVersionResolver.SelectHighestPair(
                torchIndex?.AvailableVersions,
                torchvisionIndex?.AvailableVersions
            );

            if (pair is { } selectedPair)
            {
                if (usesNightlyIndex)
                {
                    onConsoleOutput?.Invoke(
                        ProcessOutput.FromStdErrLine(
                            "ROCm: no nightly build shares the same date stamp for torch and torchvision; "
                                + "falling back to the newest builds of each."
                        )
                    );
                }

                // pip lists versions in descending order, so the first entry is the newest torch.
                var newestTorch = torchIndex?.AvailableVersions is { Count: > 0 } torchVersions
                    ? RocmTorchVersionResolver.StripLocalVersion(torchVersions[0])
                    : null;

                if (
                    !string.IsNullOrWhiteSpace(newestTorch)
                    && !string.Equals(newestTorch, selectedPair.Torch, StringComparison.OrdinalIgnoreCase)
                )
                {
                    onConsoleOutput?.Invoke(
                        ProcessOutput.FromStdErrLine(
                            $"ROCm: torch {newestTorch} has no matching torchvision build on the index; "
                                + $"using torch {selectedPair.Torch} instead."
                        )
                    );
                }

                Logger.Info(
                    "Resolved ROCm torch {TorchVersion} with torchvision {TorchvisionVersion}.",
                    selectedPair.Torch,
                    selectedPair.Torchvision
                );

                return new TorchInstallResolution(
                    $"{torchSpecifierBase}=={selectedPair.Torch}",
                    $"{torchvisionSpecifierBase}=={selectedPair.Torchvision}",
                    AllowPrerelease: false
                );
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.Warn(
                exception,
                "Failed to resolve a paired ROCm torch/torchvision set from the ROCm index."
            );
        }

        onConsoleOutput?.Invoke(
            ProcessOutput.FromStdErrLine(
                "ROCm: no paired torch/torchvision build could be resolved from the index. "
                    + "Falling back to the newest available builds."
            )
        );

        // Only widen to pre-releases on nightly, where pip would otherwise select an older final.
        return new TorchInstallResolution(
            torchSpecifierBase,
            torchvisionSpecifierBase,
            AllowPrerelease: usesNightlyIndex
        );
    }

    /// <summary>
    /// Builds a compatibility result from the current machine state and package profile.
    /// This keeps the first ROCm helper slice focused on hardware capability and GPU selection only.
    /// </summary>
    private RocmCompatibilityResult BuildCompatibilityResult()
    {
        var state = machineState.Value;

        return new RocmCompatibilityResult
        {
            IsCompatible = state.IsCompatible,
            FailureReason = state.FailureReason,
            SelectedGpu = state.SelectedGpu,
            ResolvedGfxArch = state.RuntimeGfxArch,
        };
    }

    private RocmMachineState ResolveMachineState()
    {
        if (settingsManager.Settings.PreferredGpu is { } preferredGpu && !preferredGpu.IsAmd)
        {
            return new RocmMachineState
            {
                IsCompatible = false,
                FailureReason = !string.IsNullOrWhiteSpace(preferredGpu.Name)
                    ? $"Preferred GPU '{preferredGpu.Name}' is not an AMD GPU."
                    : "Preferred GPU is not an AMD GPU.",
            };
        }

        var amdGpus = GetAmdGpuCandidates().ToList();
        if (amdGpus.Count == 0)
        {
            return new RocmMachineState
            {
                IsCompatible = false,
                FailureReason = "No AMD GPU was detected for ROCm evaluation.",
            };
        }

        var supportedAmdGpus = amdGpus.Where(RocmSupport.IsSupportedGpu).ToList();
        var linuxDevice = TrySelectLinuxDevice();
        var detectedArch = linuxDevice is null
            ? null
            : RocmSupport.TryGetCanonicalArchitecture(linuxDevice.GfxArch);
        // Prefer the driver's count when it has one, since it counts GPU agents rather than adapters.
        var driverDeviceCount = RocmArchDetector.Devices.Count;
        var amdDeviceCount = driverDeviceCount > 0 ? driverDeviceCount : amdGpus.Count;

        // The driver can resolve an architecture the adapter-name table has never heard of, so only fail
        // when neither source produced one.
        if (supportedAmdGpus.Count == 0 && detectedArch is null)
        {
            return new RocmMachineState
            {
                IsCompatible = false,
                FailureReason = "No AMD GPU with a supported ROCm architecture was detected.",
            };
        }

        var selectedGpu =
            TryResolvePreferredAmdGpu(supportedAmdGpus, settingsManager.Settings.PreferredGpu)
            ?? SelectPreferredGpu(supportedAmdGpus)
            ?? SelectPreferredGpu(amdGpus)
            ?? amdGpus[0];
        var runtimeGfxArch =
            detectedArch
            ?? RocmSupport.TryGetCanonicalArchitecture(selectedGpu.GetAmdGfxArch())
            ?? GetSupportedFallbackGfxArch(supportedAmdGpus);
        var isCompatible = !string.IsNullOrWhiteSpace(runtimeGfxArch);

        return new RocmMachineState
        {
            IsCompatible = isCompatible,
            FailureReason = isCompatible
                ? null
                : "No supported AMD GFX architecture could be resolved for ROCm.",
            SelectedGpu = selectedGpu,
            RuntimeGfxArch = runtimeGfxArch,
            AmdDeviceCount = amdDeviceCount,
            DeviceIndex = linuxDevice?.DeviceIndex,
        };
    }

    /// <summary>
    /// ISA target of the GPU the kernel reports through KFD, or null when KFD is unavailable or reports
    /// nothing supported. The driver's answer takes precedence over the adapter-name table on Linux.
    /// </summary>
    private static RocmDetectedDevice? TrySelectLinuxDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        var supportedDevices = RocmArchDetector
            .Devices.Where(device => RocmSupport.TryGetCanonicalArchitecture(device.GfxArch) is not null)
            .ToList();

        return RocmArchDetector.SelectPreferred(supportedDevices);
    }

    /// <summary>
    /// Picks between candidate adapters when the user has no preference: the one with the most memory,
    /// which separates a discrete card from an APU's shared carveout.
    /// </summary>
    internal static GpuInfo? SelectPreferredGpu(IEnumerable<GpuInfo> candidates) =>
        candidates.OrderByDescending(gpu => gpu.MemoryBytes).FirstOrDefault();

    /// <summary>
    /// Returns AMD GPUs from Stability Matrix's internal hardware model.
    /// This is the canonical GPU source for the ROCm helper and intentionally avoids package-local probing.
    /// </summary>
    private static IReadOnlyList<GpuInfo> GetAmdGpuCandidates()
    {
        return HardwareHelper.IterGpuInfo().Where(gpu => gpu.IsAmd).ToList();
    }

    /// <summary>
    /// Resolves the preferred AMD GPU when the configured preference is still present in the current hardware list.
    /// </summary>
    private static GpuInfo? TryResolvePreferredAmdGpu(
        IEnumerable<GpuInfo> availableGpus,
        GpuInfo? preferredGpu
    )
    {
        if (preferredGpu is null || !preferredGpu.IsAmd)
            return null;

        var preferredMatch = availableGpus.FirstOrDefault(gpu => gpu.Equals(preferredGpu));
        if (preferredMatch is not null)
            return preferredMatch;

        if (!string.IsNullOrWhiteSpace(preferredGpu.Name))
        {
            Logger.Info(
                "Preferred GPU {PreferredGpuName} was ignored for ROCm detection because it is not present in current hardware enumeration.",
                preferredGpu.Name
            );
        }

        return null;
    }

    /// <summary>
    /// Resolves the first supported AMD GFX architecture from the current machine state when no preferred GPU applies.
    /// </summary>
    private static string? GetSupportedFallbackGfxArch(IEnumerable<GpuInfo> availableGpus)
    {
        return availableGpus
            .Where(RocmSupport.IsSupportedGpu)
            .Select(gpu => RocmSupport.TryGetCanonicalArchitecture(gpu.GetAmdGfxArch()))
            .FirstOrDefault(RocmSupport.IsSupportedArchitecture);
    }

    /// <summary>
    /// Verifies that the installed torch build still reports usable ROCm metadata after helper-managed installs complete.
    /// </summary>
    private static async Task VerifyRocmTorchInstallAsync(
        IPyVenvRunner venvRunner,
        Action<ProcessOutput>? onConsoleOutput,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            throw new InvalidOperationException("torch was not installed after ROCm setup.");
        }

        var verificationResult = await venvRunner
            .Run(
                "-c \"import json, torch, torchvision; print(json.dumps({'version': torch.__version__, 'torchvision': torchvision.__version__, 'hip': torch.version.hip, 'cuda': torch.cuda.is_available()}))\""
            )
            .ConfigureAwait(false);

        var verificationOutput = (verificationResult.StandardOutput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(verificationOutput))
        {
            var verificationError = (verificationResult.StandardError ?? string.Empty).Trim();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(verificationError)
                    ? "Torch verification produced no output."
                    : $"Torch verification produced no output. Standard error: {verificationError}"
            );
        }

        var verificationJson = TryExtractJsonObject(verificationOutput);
        if (string.IsNullOrWhiteSpace(verificationJson))
        {
            throw new InvalidOperationException(
                $"Unexpected torch verification output: {verificationOutput}"
            );
        }

        JsonDocument verificationDocument;
        try
        {
            verificationDocument = JsonDocument.Parse(verificationJson);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Unexpected torch verification output: {verificationOutput}",
                exception
            );
        }

        using (verificationDocument)
        {
            var root = verificationDocument.RootElement;
            var version = root.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : null;
            var torchvisionVersion = root.TryGetProperty("torchvision", out var torchvisionElement)
                ? torchvisionElement.GetString()
                : null;
            var hipVersion = root.TryGetProperty("hip", out var hipElement) ? hipElement.GetString() : null;
            var cudaAvailable = root.TryGetProperty("cuda", out var cudaElement) && cudaElement.GetBoolean();

            if (!IsUsableRocmTorchBuild(version, hipVersion))
            {
                throw new InvalidOperationException(
                    $"Installed torch is not a usable ROCm build. Verification output: {verificationOutput}"
                );
            }

            // If AMD's torchvision wheels declare no torch version constraint, so an ABI-mismatched pair only
            // fails at import time. Importing it here surfaces that as an install failure instead of a
            // confusing runtime error later.
            if (string.IsNullOrWhiteSpace(torchvisionVersion))
            {
                throw new InvalidOperationException(
                    $"Installed torchvision could not be imported. Verification output: {verificationOutput}"
                );
            }

            if (!cudaAvailable)
            {
                onConsoleOutput?.Invoke(
                    ProcessOutput.FromStdErrLine(
                        $"Torch verification warning: installed ROCm torch build reported cuda={cudaAvailable}; continuing because ROCm metadata was detected (version={version}, hip={hipVersion})."
                    )
                );
            }

            onConsoleOutput?.Invoke(
                ProcessOutput.FromStdOutLine(
                    $"Torch verification: version={version}, torchvision={torchvisionVersion}, hip={hipVersion}, cuda={cudaAvailable}"
                )
            );
        }
    }

    internal static bool IsUsableRocmTorchBuild(string? version, string? hipVersion)
    {
        if (!string.IsNullOrWhiteSpace(hipVersion))
            return true;

        return !string.IsNullOrWhiteSpace(version)
            && version.Contains("rocm", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetNightlyBuildDateToken(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;

        var devIndex = version.IndexOf("dev", StringComparison.OrdinalIgnoreCase);
        if (devIndex < 0)
            return null;

        var startIndex = devIndex + 3;
        if (version.Length < startIndex + 8)
            return null;

        var token = version.Substring(startIndex, 8);
        return token.All(char.IsDigit) ? token : null;
    }

    private static bool HasNightlyBuildDateToken(string? version, string nightlyBuildDateToken)
    {
        return !string.IsNullOrWhiteSpace(version)
            && !string.IsNullOrWhiteSpace(nightlyBuildDateToken)
            && version.Contains($"dev{nightlyBuildDateToken}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ROCm version a torch build was compiled against, taken from its local label: 2.14.0+rocm10.0.0
    /// becomes 10.0.0. Returns null when the build carries no rocm local label.
    /// </summary>
    internal static string? TryGetRocmVersionLabel(string? torchVersion)
    {
        if (string.IsNullOrWhiteSpace(torchVersion))
        {
            return null;
        }

        var plusIndex = torchVersion.IndexOf('+');
        if (plusIndex < 0 || plusIndex == torchVersion.Length - 1)
        {
            return null;
        }

        var localLabel = torchVersion[(plusIndex + 1)..].Trim();
        const string prefix = "rocm";
        if (!localLabel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var version = localLabel[prefix.Length..].Trim();
        return version.Length == 0 ? null : version;
    }

    /// <summary>True when a rocm-sdk-devel build sits on the given ROCm version line.</summary>
    internal static bool IsRocmSdkDevelMatch(string? sdkDevelVersion, string? rocmVersion)
    {
        return !string.IsNullOrWhiteSpace(sdkDevelVersion)
            && !string.IsNullOrWhiteSpace(rocmVersion)
            && string.Equals(sdkDevelVersion.Trim(), rocmVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A rocm-sdk-devel build to install, and whether it matched the torch build exactly.</summary>
    internal readonly record struct SdkDevelSelection(string Version, bool IsExactMatch);

    /// <summary>
    /// Chooses the rocm-sdk-devel build for a torch install: the nightly build date token first, then the
    /// ROCm line of the torch wheel, and the newest published build only when neither matches.
    /// </summary>
    internal static SdkDevelSelection? SelectSdkDevelVersion(
        IReadOnlyList<string>? availableVersions,
        string? rocmVersion,
        string? nightlyBuildDateToken
    )
    {
        if (availableVersions is null || availableVersions.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(nightlyBuildDateToken))
        {
            var nightly = availableVersions.FirstOrDefault(version =>
                HasNightlyBuildDateToken(version, nightlyBuildDateToken)
            );

            if (nightly is not null)
            {
                return new SdkDevelSelection(nightly, true);
            }
        }

        if (!string.IsNullOrWhiteSpace(rocmVersion))
        {
            var exact = availableVersions.FirstOrDefault(version =>
                IsRocmSdkDevelMatch(version, rocmVersion)
            );

            if (exact is not null)
            {
                return new SdkDevelSelection(exact, true);
            }
        }

        return new SdkDevelSelection(availableVersions[0], false);
    }

    internal static string? TryExtractJsonObject(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var trimmedOutput = output.Trim();

        for (var index = 0; index < trimmedOutput.Length; index++)
        {
            if (trimmedOutput[index] != '{')
                continue;

            try
            {
                using var document = JsonDocument.Parse(trimmedOutput[index..]);
                return document.RootElement.GetRawText();
            }
            catch (JsonException) { }
        }

        return null;
    }

    /// <summary>
    /// Builds helper-owned ROCm launch variables from the resolved runtime context and package profile.
    /// </summary>
    private IReadOnlyDictionary<string, string> BuildHelperLaunchEnvironment(
        RocmRuntimeContext runtimeContext,
        RocmPackageProfile profile
    )
    {
        var environment = new Dictionary<string, string>(EnvComparer);
        var options = profile.EnvironmentOptions;
        var gfxArch = runtimeContext.RuntimeGfxArch;

        ApplyDefaultLaunchEnvironment(environment, gfxArch, options);
        ApplyDeviceVisibility(environment, runtimeContext);

        return environment;
    }

    /// <summary>
    /// Pins the runtime to the GPU the helper selected when the machine exposes more than one, so an APU
    /// that enumerates first cannot become the device an app opens. <c>ROCR_VISIBLE_DEVICES</c> is the
    /// lowest device filter and the runtime renumbers what remains from zero. Single-GPU machines are
    /// left alone, as are machines whose selection has no device index.
    /// </summary>
    internal static void ApplyDeviceVisibility(
        IDictionary<string, string> environment,
        RocmRuntimeContext runtimeContext
    )
    {
        if (runtimeContext.AmdDeviceCount <= 1 || runtimeContext.DeviceIndex is not { } deviceIndex)
        {
            return;
        }

        environment["ROCR_VISIBLE_DEVICES"] = deviceIndex.ToString();
        Logger.Info(
            "Pinning ROCm device visibility to device {Index} of {Count}.",
            deviceIndex,
            runtimeContext.AmdDeviceCount
        );
    }

    private void ApplyDefaultLaunchEnvironment(
        IDictionary<string, string> environment,
        string? gfxArch,
        RocmEnvironmentOptions options
    )
    {
        SetIfNotNull(environment, "FLASH_ATTENTION_TRITON_AMD_ENABLE", options.FlashAttentionTritonAmdEnable);
        SetIfNotNull(environment, "MIOPEN_FIND_MODE", options.MiopenFindMode);
        SetIfNotNull(environment, "MIOPEN_SEARCH_CUTOFF", options.MiopenSearchCutoff);
        SetIfNotNull(environment, "MIOPEN_FIND_ENFORCE", options.MiopenFindEnforce);
        SetIfNotNull(environment, "PYTORCH_ALLOC_CONF", options.PyTorchAllocConf);

        if (options.ApplyAotritonExperimental && RocmSupport.SupportsAotritonExperimental(gfxArch))
        {
            environment["TORCH_ROCM_AOTRITON_ENABLE_EXPERIMENTAL"] = "1";
        }

        if (options.ApplyLegacySdpFallback && RocmSupport.PreferMathSdpaFallback(gfxArch))
        {
            environment["TORCH_BACKENDS_CUDA_FLASH_SDP_ENABLED"] = "0";
            environment["TORCH_BACKENDS_CUDA_MEM_EFF_SDP_ENABLED"] = "0";
            environment["TORCH_BACKENDS_CUDA_MATH_SDP_ENABLED"] = "1";
        }
    }

    private static void SetIfNotNull(IDictionary<string, string> environment, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            environment[key] = value;
        }
    }

    /// <summary>
    /// Merges helper-owned and package-specific launch environment variables.
    /// </summary>
    private IReadOnlyDictionary<string, string> MergeLaunchEnvironment(
        IReadOnlyDictionary<string, string> helperEnvironment,
        IReadOnlyDictionary<string, string> packageEnvironment,
        RocmEnvironmentOptions options
    )
    {
        var merged = new Dictionary<string, string>(EnvComparer);

        foreach (var source in new[] { helperEnvironment, packageEnvironment })
        {
            if (ReferenceEquals(source, packageEnvironment) && !options.IncludePackageOverrides)
                continue;

            foreach (var pair in source)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        // Neutralize inherited system ROCm/HIP SDK variables so the venv's self-contained SDK is used.
        // User overrides below can still intentionally re-set these keys.
        if (options.SanitizeRocmSdkEnvironment)
        {
            ApplyRocmSdkEnvironmentSanitization(merged);
        }

        if (
            options.IncludeUserOverrides
            && settingsManager.Settings.EnvironmentVariables is { Count: > 0 } userOverrides
        )
        {
            foreach (var pair in userOverrides)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    /// <summary>
    /// Marks the inherited system ROCm/HIP SDK variables for removal from the child process, so the
    /// venv's self-contained SDK wins over any system install that happens to be on PATH.
    /// </summary>
    internal static void ApplyRocmSdkEnvironmentSanitization(IDictionary<string, string> environment)
    {
        foreach (var key in RocmSupport.ConflictingRocmEnvironmentVariables)
        {
            environment[key] = ProcessRunner.UnsetEnvironmentVariable;
        }
    }
}
