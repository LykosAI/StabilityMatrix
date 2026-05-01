using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Centralizes Windows ROCm support policy so hardware detection, package selection,
/// and ROCm installation all use the same architecture support map.
/// </summary>
public static class WindowsRocmSupport
{
    public static bool IsSupportedGpu(GpuInfo? gpu)
    {
        if (gpu is null || !gpu.IsAmd || string.IsNullOrWhiteSpace(gpu.Name))
            return false;

        return IsSupportedArchitecture(gpu.GetAmdGfxArch());
    }

    public static bool IsSupportedArchitecture(string? gfxArch)
    {
        return TryGetPackageIndexUrl(gfxArch) is not null;
    }

    public static string? TryGetPackageIndexUrl(string? gfxArch)
    {
        return gfxArch switch
        {
            "gfx900" => "https://rocm.nightlies.amd.com/v2-staging/gfx900/", // Vega 10
            "gfx906" => "https://rocm.nightlies.amd.com/v2-staging/gfx906/", // Radeon VII, Vega 20
            "gfx90X" => "https://rocm.nightlies.amd.com/v2-staging/gfx90X/", // Radeon Pro VII
            var s when s != null && s.StartsWith("gfx101", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2-staging/gfx101X-dgpu/", // RDNA1 (5000 series, Pro)
            var s when s != null && s.StartsWith("gfx103", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2-staging/gfx103X-all/", // RDNA2 (6000 series, 6xxM Mobile, Steam Deck)
            var s when s != null && s.StartsWith("gfx110", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2/gfx110X-all/", // RDNA3 (7000 series, 7xxM Mobile)
            "gfx1150" => "https://rocm.nightlies.amd.com/v2-staging/gfx1150/", // RDNA3.5 (Strix/Gorgon Point)
            "gfx1151" => "https://rocm.nightlies.amd.com/v2/gfx1151/", // RDNA3.5 (Strix Halo)
            "gfx1152" => "https://rocm.nightlies.amd.com/v2-staging/gfx1152/", // RDNA3.5 (Kraken Point)
            "gfx1153" => "https://rocm.nightlies.amd.com/v2-staging/gfx1153/", // RDNA3.5 (Medusa Point)
            var s when s != null && s.StartsWith("gfx120", StringComparison.OrdinalIgnoreCase) =>
                "https://rocm.nightlies.amd.com/v2/gfx120X-all/", // RDNA4 (9000 series)
            _ => null,
        };
    }
}
