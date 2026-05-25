using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Centralizes Windows ROCm support and architecture policy so hardware detection, package selection,
/// installation, and shared launch decisions use the same support map.
/// </summary>
public static class WindowsRocmSupport
{
    public const string MultiArchPythonPackageIndexUrl = "https://repo.amd.com/rocm/whl-multi-arch/";

    // Used to exclude modern gfxarches from AOTriton activation EnVar as AOTriton does not currently support them.
    // This is a temporary measure until AOTriton adds support for these architectures.
    private static readonly HashSet<string> AotritonExperimentalExcludedArchitectures =
    [
        "gfx1152",
        "gfx1153",
    ];

    public static bool IsSupportedGpu(GpuInfo? gpu)
    {
        if (gpu is null || !gpu.IsAmd || string.IsNullOrWhiteSpace(gpu.Name))
            return false;

        return IsSupportedArchitecture(gpu.GetAmdGfxArch());
    }

    public static bool IsSupportedArchitecture(string? gfxArch)
    {
        return TryGetCanonicalArchitecture(gfxArch) is not null;
    }

    public static bool IsModernArchitecture(string? gfxArch)
    {
        return gfxArch?.StartsWith("gfx110", StringComparison.OrdinalIgnoreCase) == true
            || gfxArch?.StartsWith("gfx115", StringComparison.OrdinalIgnoreCase) == true
            || gfxArch?.StartsWith("gfx120", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool SupportsAotritonExperimental(string? gfxArch)
    {
        var canonicalArch = TryGetCanonicalArchitecture(gfxArch);
        return canonicalArch is not null
            && IsModernArchitecture(canonicalArch)
            && !AotritonExperimentalExcludedArchitectures.Contains(canonicalArch);
    }

    public static bool IsLegacyArchitecture(string? gfxArch)
    {
        return IsSupportedArchitecture(gfxArch) && !IsModernArchitecture(gfxArch);
    }

    public static bool PreferLegacyAttentionFallback(string? gfxArch)
    {
        return IsLegacyArchitecture(gfxArch);
    }

    public static string? TryGetCanonicalArchitecture(string? gfxArch)
    {
        if (string.IsNullOrWhiteSpace(gfxArch))
            return null;

        var normalizedArch = gfxArch.ToLowerInvariant();

        return normalizedArch switch
        {
            "gfx900" or "gfx906" or "gfx1150" or "gfx1151" or "gfx1152" or "gfx1153" => normalizedArch,
            var s
                when s.StartsWith("gfx101", StringComparison.Ordinal)
                    || s.StartsWith("gfx103", StringComparison.Ordinal)
                    || s.StartsWith("gfx110", StringComparison.Ordinal)
                    || s.StartsWith("gfx120", StringComparison.Ordinal) => normalizedArch,
            _ => null,
        };
    }

    public static string? TryGetMultiArchDeviceExtra(string? gfxArch)
    {
        var canonicalArch = TryGetCanonicalArchitecture(gfxArch);
        return canonicalArch is null ? null : $"device-{canonicalArch}";
    }
}
