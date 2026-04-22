using System.Collections.Immutable;
using System.Text.Json;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
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
public class RocmPackageHelper(ISettingsManager settingsManager) : IRocmPackageHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly string[] UnsupportedRdna2ModelMarkers =
    [
        "680m",
        "660m",
        "610m",
        "rx6300",
        "w6300",
        "rx6400",
        "w6400",
        "rx6450",
        "rx6550",
    ];

    /// <inheritdoc />
    public Task<RocmCompatibilityResult> GetCompatibilityAsync(
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(BuildCompatibilityResult(profile));
    }

    /// <inheritdoc />
    public Task<RocmRuntimeContext> ResolveRuntimeContextAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        var compatibility = BuildCompatibilityResult(profile);
        if (!compatibility.IsCompatible)
        {
            return Task.FromResult(
                new RocmRuntimeContext
                {
                    IsSupported = false,
                    FailureReason = compatibility.FailureReason,
                    SelectedGpu = compatibility.SelectedGpu,
                    RuntimeGfxArch = compatibility.ResolvedGfxArch,
                }
            );
        }

        var supportedAmdGpus = GetAmdGpuCandidates(forceRefresh: true)
            .Where(IsSupportedWindowsRocmGpu)
            .ToList();

        var selectedGpu =
            compatibility.SelectedGpu
            ?? TryResolvePreferredAmdGpu(supportedAmdGpus, settingsManager.Settings.PreferredGpu)
            ?? supportedAmdGpus.FirstOrDefault();

        var runtimeGfxArch =
            compatibility.ResolvedGfxArch
            ?? selectedGpu?.GetAmdGfxArch()
            ?? GetSupportedFallbackGfxArch(supportedAmdGpus);

        return Task.FromResult(
            new RocmRuntimeContext
            {
                IsSupported = true,
                SelectedGpu = selectedGpu,
                RuntimeGfxArch = runtimeGfxArch,
                IsLegacyGpu = IsLegacyArchitecture(runtimeGfxArch),
                IsRdna1 = IsRdna1Architecture(runtimeGfxArch),
            }
        );
    }

    /// <inheritdoc />
    public Task<RocmInstallContext> ResolveInstallContextAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        _ = installLocation;
        _ = installedPackage;
        _ = cancellationToken;

        var supportedAmdGpus = GetAmdGpuCandidates(forceRefresh: true)
            .Where(IsSupportedWindowsRocmGpu)
            .ToList();

        var preferredGfxArch = TryResolvePreferredAmdGfxArch(
            supportedAmdGpus,
            settingsManager.Settings.PreferredGpu
        );

        var runtimeGfxArch = preferredGfxArch ?? GetSupportedFallbackGfxArch(supportedAmdGpus);
        var windowsNativeIndexUrl = TryGetWindowsNativeRocmIndexUrl(runtimeGfxArch);

        return Task.FromResult(
            new RocmInstallContext
            {
                PreferredGfxArch = preferredGfxArch,
                RuntimeGfxArch = runtimeGfxArch,
                RocmPackageIndexUrl = windowsNativeIndexUrl,
                RocmTorchIndexUrl = windowsNativeIndexUrl,
            }
        );
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> BuildInstallEnvironment(
        string installLocation,
        RocmInstallContext context,
        RocmPackageProfile profile
    )
    {
        _ = installLocation;
        _ = context;
        _ = profile;
        return new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public Task<RocmInstallContext> RefreshPackageAfterUpdateAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        return ResolveInstallContextAsync(installLocation, installedPackage, profile, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> BuildLaunchEnvironmentAsync(
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        _ = installLocation;
        _ = installedPackage;

        var runtimeContext = ResolveRuntimeContextAsync(
                installLocation,
                installedPackage,
                profile,
                cancellationToken
            )
            .GetAwaiter()
            .GetResult();

        if (!runtimeContext.IsSupported)
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        var helperEnvironment = BuildHelperLaunchEnvironment(runtimeContext, profile);
        var packageEnvironment =
            profile.ExtraEnvironmentFactory?.Invoke(runtimeContext) ?? new Dictionary<string, string>();

        var mergedEnvironment = MergeLaunchEnvironment(
            helperEnvironment,
            packageEnvironment,
            profile.EnvironmentOptions
        );

        return Task.FromResult<IReadOnlyDictionary<string, string>>(mergedEnvironment);
    }

    /// <inheritdoc />
    public async Task ApplyLaunchEnvironmentAsync(
        IPyVenvRunner venvRunner,
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        var environment = await BuildLaunchEnvironmentAsync(
                installLocation,
                installedPackage,
                profile,
                cancellationToken
            )
            .ConfigureAwait(false);

        venvRunner.UpdateEnvironmentVariables(env => env.SetItems(environment));
    }

    /// <inheritdoc />
    public async Task InstallWindowsNativePackageAsync(
        IPyVenvRunner venvRunner,
        string installLocation,
        InstalledPackage installedPackage,
        RocmPackageProfile profile,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null,
        CancellationToken cancellationToken = default
    )
    {
        var compatibility = await GetCompatibilityAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!compatibility.IsCompatible)
        {
            throw new ApplicationException(
                compatibility.FailureReason
                    ?? "Windows ROCm installation is not supported for the current machine."
            );
        }

        var installContext = await ResolveInstallContextAsync(
                installLocation,
                installedPackage,
                profile,
                cancellationToken
            )
            .ConfigureAwait(false);

        var rocmPackageIndexUrl = installContext.RocmPackageIndexUrl;
        var rocmTorchIndexUrl = installContext.RocmTorchIndexUrl ?? rocmPackageIndexUrl;

        if (string.IsNullOrWhiteSpace(rocmPackageIndexUrl) || string.IsNullOrWhiteSpace(rocmTorchIndexUrl))
        {
            throw new ApplicationException(
                $"No Windows ROCm Technical Preview index URL is available for '{installContext.RuntimeGfxArch ?? "unknown"}'."
            );
        }

        progress?.Report(new ProgressReport(-1f, "Upgrading pip...", isIndeterminate: true));
        await venvRunner.PipInstall("--upgrade pip wheel", onConsoleOutput).ConfigureAwait(false);

        if (profile.RequiresRocmSdk)
        {
            progress?.Report(new ProgressReport(-1f, "Installing ROCm runtime...", isIndeterminate: true));
            var rocmRuntimeArgs = new PipInstallArgs()
                .AddKeyedArgs("--index-url", ["--index-url", rocmPackageIndexUrl])
                .AddArgs("rocm[devel,libraries]", "--no-warn-script-location");

            if (installedPackage.PipOverrides != null)
            {
                rocmRuntimeArgs = rocmRuntimeArgs.WithUserOverrides(installedPackage.PipOverrides);
            }

            await venvRunner.PipInstall(rocmRuntimeArgs, onConsoleOutput).ConfigureAwait(false);

            progress?.Report(new ProgressReport(-1f, "Initializing ROCm SDK...", isIndeterminate: true));
            var rocmSdkExe = Path.Combine(installLocation, "venv", "Scripts", "rocm-sdk.exe");
            if (!File.Exists(rocmSdkExe))
            {
                throw new FileNotFoundException("rocm-sdk.exe was not installed", rocmSdkExe);
            }

            using var rocmSdkProcess = ProcessRunner.StartAnsiProcess(
                rocmSdkExe,
                ["init"],
                installLocation,
                onConsoleOutput
            );

            await rocmSdkProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (rocmSdkProcess.ExitCode != 0)
            {
                throw new ProcessException($"rocm-sdk init failed with code {rocmSdkProcess.ExitCode}");
            }
        }

        progress?.Report(new ProgressReport(-1f, "Installing ROCm torch...", isIndeterminate: true));
        var torchArgs = new PipInstallArgs()
            .AddKeyedArgs("--index-url", ["--index-url", rocmTorchIndexUrl])
            .AddArgs("torch", "torchaudio", "torchvision", "--no-warn-script-location");

        if (profile.ForceReinstallTorch)
        {
            torchArgs = torchArgs.AddArg("--force-reinstall");
        }

        if (installedPackage.PipOverrides != null)
        {
            torchArgs = torchArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(torchArgs, onConsoleOutput).ConfigureAwait(false);

        progress?.Report(
            new ProgressReport(-1f, "Installing package requirements...", isIndeterminate: true)
        );

        var requirementsPipArgs = new PipInstallArgs([.. profile.ExtraInstallPipArgs]);
        if (profile.UpgradePackages)
        {
            requirementsPipArgs = requirementsPipArgs.AddArg("--upgrade");
        }

        foreach (var relativePath in profile.RequirementsFilePaths)
        {
            var requirementsFile = new FilePath(venvRunner.WorkingDirectory ?? installLocation, relativePath);
            if (!requirementsFile.Exists)
                continue;

            var requirementsContent = await requirementsFile
                .ReadAllTextAsync(cancellationToken)
                .ConfigureAwait(false);

            requirementsPipArgs = requirementsPipArgs.WithParsedFromRequirementsTxt(
                requirementsContent,
                profile.RequirementsExcludePattern
            );
        }

        if (installedPackage.PipOverrides != null)
        {
            requirementsPipArgs = requirementsPipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(requirementsPipArgs, onConsoleOutput).ConfigureAwait(false);

        if (!profile.PostInstallPipArgs.Any())
            return;

        var postInstallPipArgs = new PipInstallArgs([.. profile.PostInstallPipArgs]);
        if (installedPackage.PipOverrides != null)
        {
            postInstallPipArgs = postInstallPipArgs.WithUserOverrides(installedPackage.PipOverrides);
        }

        await venvRunner.PipInstall(postInstallPipArgs, onConsoleOutput).ConfigureAwait(false);

        await VerifyWindowsNativeTorchInstallAsync(venvRunner, onConsoleOutput).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a compatibility result from the current machine state and package profile.
    /// This keeps the first ROCm helper slice focused on hardware capability and GPU selection only.
    /// </summary>
    private RocmCompatibilityResult BuildCompatibilityResult(RocmPackageProfile profile)
    {
        if (profile.RequiresWindows && !Compat.IsWindows)
        {
            return new RocmCompatibilityResult
            {
                IsCompatible = false,
                FailureReason = "This ROCm profile currently requires Windows.",
            };
        }

        var amdGpus = GetAmdGpuCandidates(forceRefresh: true).ToList();
        if (amdGpus.Count == 0)
        {
            return new RocmCompatibilityResult
            {
                IsCompatible = false,
                FailureReason = "No AMD GPU was detected for ROCm evaluation.",
            };
        }

        var preferredGpu = settingsManager.Settings.PreferredGpu;
        if (preferredGpu is not null && IsExplicitlyUnsupportedRdna2Gpu(preferredGpu))
        {
            return new RocmCompatibilityResult
            {
                IsCompatible = false,
                FailureReason = $"Selected GPU '{preferredGpu.Name}' is unsupported for Windows ROCm.",
                SelectedGpu = preferredGpu,
            };
        }

        var supportedAmdGpus = amdGpus.Where(IsSupportedWindowsRocmGpu).ToList();
        if (supportedAmdGpus.Count == 0)
        {
            return new RocmCompatibilityResult
            {
                IsCompatible = false,
                FailureReason = GetUnsupportedGpuReason(amdGpus),
            };
        }

        var selectedGpu =
            TryResolvePreferredAmdGpu(supportedAmdGpus, preferredGpu) ?? supportedAmdGpus.First();
        var resolvedGfxArch = selectedGpu.GetAmdGfxArch() ?? GetSupportedFallbackGfxArch(supportedAmdGpus);

        return new RocmCompatibilityResult
        {
            IsCompatible = !string.IsNullOrWhiteSpace(resolvedGfxArch),
            FailureReason = string.IsNullOrWhiteSpace(resolvedGfxArch)
                ? "No supported AMD GFX architecture could be resolved for ROCm."
                : null,
            SelectedGpu = selectedGpu,
            ResolvedGfxArch = resolvedGfxArch,
        };
    }

    /// <summary>
    /// Returns AMD GPUs from Stability Matrix's internal hardware model.
    /// This is the canonical GPU source for the ROCm helper and intentionally avoids package-local probing.
    /// </summary>
    private static IReadOnlyList<GpuInfo> GetAmdGpuCandidates(bool forceRefresh = false)
    {
        return HardwareHelper.IterGpuInfo(forceRefresh).Where(gpu => gpu.IsAmd).ToList();
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
    /// Resolves the preferred AMD GFX architecture when the configured GPU is supported and currently present.
    /// </summary>
    private static string? TryResolvePreferredAmdGfxArch(
        IEnumerable<GpuInfo> availableGpus,
        GpuInfo? preferredGpu
    )
    {
        var resolvedPreferredGpu = TryResolvePreferredAmdGpu(availableGpus, preferredGpu);
        return resolvedPreferredGpu is not null && IsSupportedWindowsRocmGpu(resolvedPreferredGpu)
            ? resolvedPreferredGpu.GetAmdGfxArch()
            : null;
    }

    /// <summary>
    /// Resolves the first supported AMD GFX architecture from the current machine state when no preferred GPU applies.
    /// </summary>
    private static string? GetSupportedFallbackGfxArch(IEnumerable<GpuInfo> availableGpus)
    {
        return availableGpus
            .Where(IsSupportedWindowsRocmGpu)
            .Select(gpu => gpu.GetAmdGfxArch())
            .FirstOrDefault(IsSupportedWindowsRocmArchitecture);
    }

    /// <summary>
    /// Determines whether a GPU is supported by the Windows ROCm install flow currently modeled by the helper.
    /// Unsupported low-end RDNA2/APU models are filtered explicitly even when they identify as AMD hardware.
    /// </summary>
    private static bool IsSupportedWindowsRocmGpu(GpuInfo gpu)
    {
        if (!gpu.IsAmd || string.IsNullOrWhiteSpace(gpu.Name))
            return false;

        if (IsExplicitlyUnsupportedRdna2Gpu(gpu))
            return false;

        return TryGetWindowsNativeRocmIndexUrl(gpu.GetAmdGfxArch()) is not null;
    }

    /// <summary>
    /// Identifies Windows ROCm-incompatible RDNA2 models that need to remain outside the supported GPU set.
    /// </summary>
    private static bool IsExplicitlyUnsupportedRdna2Gpu(GpuInfo gpu)
    {
        if (!gpu.IsAmd || string.IsNullOrWhiteSpace(gpu.Name))
            return false;

        var normalizedName = gpu.Name.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return UnsupportedRdna2ModelMarkers.Any(normalizedName.Contains);
    }

    /// <summary>
    /// Determines whether a resolved AMD GFX architecture falls inside the Windows ROCm support set currently modeled by the helper.
    /// </summary>
    private static bool IsSupportedWindowsRocmArchitecture(string? gfxArch)
    {
        return TryGetWindowsNativeRocmIndexUrl(gfxArch) is not null;
    }

    /// <summary>
    /// Maps an AMD GFX architecture identifier to the Windows-native ROCm Technical Preview feed URL.
    /// </summary>
    private static string? TryGetWindowsNativeRocmIndexUrl(string? gfxArch)
    {
        return gfxArch switch
        {
            var s when s != null && s.StartsWith("gfx101", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2-staging/gfx101X-dgpu/",
            var s when s != null && s.StartsWith("gfx103", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2-staging/gfx103X-dgpu/",
            var s when s != null && s.StartsWith("gfx110", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2/gfx110X-all/",
            "gfx1150" => "https://rocm.nightlies.amd.com/v2-staging/gfx1150/",
            "gfx1151" => "https://rocm.nightlies.amd.com/v2/gfx1151/",
            "gfx1152" => "https://rocm.nightlies.amd.com/v2-staging/gfx1152/",
            "gfx1153" => "https://rocm.nightlies.amd.com/v2-staging/gfx1153/",
            var s when s != null && s.StartsWith("gfx120", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2/gfx120X-all/",
            _ => null,
        };
    }

    /// <summary>
    /// Returns true for architectures that need the legacy ROCm runtime path.
    /// </summary>
    private static bool IsLegacyArchitecture(string? gfxArch)
    {
        return gfxArch is not null
            && (
                gfxArch.StartsWith("gfx101", StringComparison.OrdinalIgnoreCase)
                || gfxArch.StartsWith("gfx103", StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <summary>
    /// Returns true for RDNA1 architectures that need dedicated override handling.
    /// </summary>
    private static bool IsRdna1Architecture(string? gfxArch)
    {
        return gfxArch?.StartsWith("gfx101", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Produces a readable incompatibility reason when AMD hardware is present but not usable for Windows ROCm.
    /// </summary>
    private static string GetUnsupportedGpuReason(IReadOnlyList<GpuInfo> amdGpus)
    {
        if (amdGpus.Any(IsExplicitlyUnsupportedRdna2Gpu))
        {
            return "Detected only unsupported AMD RDNA2 GPUs for Windows ROCm. Unsupported models include Radeon 680M/660M/610M and RX 6300/6400/6450/6550-class GPUs.";
        }

        return "No AMD GPU with a supported Windows ROCm architecture was detected.";
    }

    /// <summary>
    /// Verifies that the installed torch build still reports a usable ROCm runtime after helper-managed installs complete.
    /// </summary>
    private static async Task VerifyWindowsNativeTorchInstallAsync(
        IPyVenvRunner venvRunner,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        var torchInfo = await venvRunner.PipShow("torch").ConfigureAwait(false);
        if (torchInfo is null)
        {
            throw new ApplicationException("torch was not installed after Windows ROCm setup.");
        }

        var verificationResult = await venvRunner
            .Run(
                "-c \"import json, torch; print(json.dumps({'version': torch.__version__, 'hip': torch.version.hip, 'cuda': torch.cuda.is_available()}))\""
            )
            .ConfigureAwait(false);

        var verificationOutput = (verificationResult.StandardOutput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(verificationOutput))
        {
            throw new ApplicationException("Torch verification produced no output.");
        }

        JsonDocument verificationDocument;
        try
        {
            verificationDocument = JsonDocument.Parse(verificationOutput);
        }
        catch (Exception exception)
        {
            throw new ApplicationException(
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
            var hipVersion = root.TryGetProperty("hip", out var hipElement) ? hipElement.GetString() : null;
            var cudaAvailable = root.TryGetProperty("cuda", out var cudaElement) && cudaElement.GetBoolean();

            if (string.IsNullOrWhiteSpace(hipVersion) || !cudaAvailable)
            {
                throw new ApplicationException(
                    $"Installed torch is not a usable ROCm build. Verification output: {verificationOutput}"
                );
            }

            onConsoleOutput?.Invoke(
                ProcessOutput.FromStdOutLine(
                    $"Torch verification: version={version}, hip={hipVersion}, cuda={cudaAvailable}"
                )
            );
        }
    }

    /// <summary>
    /// Builds helper-owned ROCm launch variables from the resolved runtime context and package profile.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildHelperLaunchEnvironment(
        RocmRuntimeContext runtimeContext,
        RocmPackageProfile profile
    )
    {
        var environment = new Dictionary<string, string>();

        if (profile.NeedsTunableOpCache)
        {
            environment["PYTORCH_TUNABLEOP_ENABLED"] = "1";
        }

        if (profile.NeedsAotritonExperimental)
        {
            environment["TORCH_ROCM_AOTRITON_ENABLE_EXPERIMENTAL"] = "1";
        }

        if (profile.NeedsTritonOverrideArch && !string.IsNullOrWhiteSpace(runtimeContext.RuntimeGfxArch))
        {
            environment["HSA_OVERRIDE_GFX_VERSION"] = runtimeContext.RuntimeGfxArch;
        }

        return environment;
    }

    /// <summary>
    /// Merges helper-owned and package-specific launch environment variables using the profile overlay rules.
    /// </summary>
    private static IReadOnlyDictionary<string, string> MergeLaunchEnvironment(
        IReadOnlyDictionary<string, string> helperEnvironment,
        IReadOnlyDictionary<string, string> packageEnvironment,
        RocmEnvironmentOptions options
    )
    {
        var merged = new Dictionary<string, string>();

        IReadOnlyDictionary<string, string>[] orderedSources =
            options.OverlayPriority == RocmEnvironmentOverlayPriority.HelperThenUserThenPackage
                ? new[] { helperEnvironment, packageEnvironment }
                : new[] { helperEnvironment, packageEnvironment };

        foreach (var source in orderedSources)
        {
            if (ReferenceEquals(source, packageEnvironment) && !options.IncludePackageOverrides)
                continue;

            foreach (var pair in source)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }
}
