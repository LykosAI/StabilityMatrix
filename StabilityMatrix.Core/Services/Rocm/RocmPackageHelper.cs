using System.Collections.Immutable;
using Injectio.Attributes;
using NLog;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Rocm;
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

    private const string EnvironmentNotImplementedMessage =
        "ROCm helper environment composition has not been implemented yet.";

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
        var supportedAmdGpus = GetAmdGpuCandidates(forceRefresh: true)
            .Where(IsSupportedWindowsRocmGpu)
            .ToList();

        var preferredGfxArch = TryResolvePreferredAmdGfxArch(
            supportedAmdGpus,
            settingsManager.Settings.PreferredGpu
        );

        return Task.FromResult(
            new RocmInstallContext
            {
                PreferredGfxArch = preferredGfxArch,
                RuntimeGfxArch = preferredGfxArch ?? GetSupportedFallbackGfxArch(supportedAmdGpus),
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
        _ = profile;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
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

        return IsSupportedWindowsRocmArchitecture(gpu.GetAmdGfxArch());
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
        return gfxArch switch
        {
            var s when s != null && s.StartsWith("gfx101", StringComparison.OrdinalIgnoreCase) => true,
            var s when s != null && s.StartsWith("gfx103", StringComparison.OrdinalIgnoreCase) => true,
            var s when s != null && s.StartsWith("gfx110", StringComparison.OrdinalIgnoreCase) => true,
            "gfx1150" or "gfx1151" or "gfx1152" or "gfx1153" => true,
            var s when s != null && s.StartsWith("gfx120", StringComparison.OrdinalIgnoreCase) => true,
            _ => false,
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
}
