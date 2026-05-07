using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Centralizes Windows ROCm support and architecture policy so hardware detection, package selection,
/// installation, and shared launch decisions use the same support map.
/// </summary>
public static class WindowsRocmSupport
{
    public const string MultiArchPythonPackageIndexUrl =
        "https://rocm.nightlies.amd.com/whl-staging-multi-arch/";

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

    public static bool IsLegacyArchitecture(string? gfxArch)
    {
        return IsSupportedArchitecture(gfxArch) && !IsModernArchitecture(gfxArch);
    }

    public static bool PreferLegacyAttentionFallback(string? gfxArch)
    {
        return IsLegacyArchitecture(gfxArch);
    }

    public static bool IsRdna1Architecture(string? gfxArch)
    {
        return gfxArch?.StartsWith("gfx101", StringComparison.OrdinalIgnoreCase) == true;
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
