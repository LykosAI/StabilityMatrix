using System.Runtime.InteropServices;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models.Settings;
using Size = StabilityMatrix.Core.Helper.Size;

namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

/// <summary>
/// Hardware summary: GPUs, system memory, CPU architecture and NVIDIA driver version. Memory
/// sizes are whole gigabytes. <see cref="DetailLevel"/> says how the values were coarsened.
/// </summary>
public class HardwareAnalyticsRequest : AnalyticsRequest
{
    /// <summary>Video memory is rounded down to this step in minimal mode.</summary>
    public const int MinimalGpuMemoryStepGb = 4;

    /// <summary>System memory is rounded down to this step in minimal mode.</summary>
    public const int MinimalSystemMemoryStepGb = 8;

    public override string Type { get; set; } = "hardware";

    /// <summary>"detailed" or "minimal", lower-cased <see cref="HardwareDetailLevel"/>.</summary>
    public string DetailLevel { get; set; } = "detailed";

    public IReadOnlyList<GpuAnalyticsInfo> Gpus { get; set; } = [];

    public string? NvidiaDriverVersion { get; set; }

    public int? TotalMemoryGb { get; set; }

    public string? ProcessArchitecture { get; set; }

    /// <summary>
    /// Builds the request from the running machine. Probes GPUs, which can take a moment on the
    /// first call, so callers should stay off the UI thread.
    /// </summary>
    public static HardwareAnalyticsRequest FromCurrentSystem(HardwareDetailLevel detailLevel)
    {
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        var driverVersion = gpus.Any(gpu => gpu.IsNvidia) ? HardwareHelper.GetNvidiaDriverVersion() : null;
        MemoryInfo? memory = HardwareHelper.TryGetMemoryInfo(out var memoryInfo) ? memoryInfo : null;

        return Create(gpus, memory, driverVersion, RuntimeInformation.ProcessArchitecture, detailLevel);
    }

    public static HardwareAnalyticsRequest Create(
        IEnumerable<GpuInfo> gpus,
        MemoryInfo? memory,
        Version? nvidiaDriverVersion,
        Architecture processArchitecture,
        HardwareDetailLevel detailLevel
    )
    {
        var isMinimal = detailLevel == HardwareDetailLevel.Minimal;
        var totalMemoryBytes = memory is { } m ? Math.Max(m.TotalInstalledBytes, m.TotalPhysicalBytes) : 0ul;
        var totalMemoryGb = totalMemoryBytes > 0 ? ToWholeGigabytes(totalMemoryBytes) : (int?)null;

        return new HardwareAnalyticsRequest
        {
            DetailLevel = detailLevel.ToString().ToLowerInvariant(),
            Gpus = gpus.Select(gpu => GpuAnalyticsInfo.FromGpuInfo(gpu, isMinimal)).ToList(),
            NvidiaDriverVersion = isMinimal
                ? nvidiaDriverVersion?.Major.ToString()
                : nvidiaDriverVersion?.ToString(),
            TotalMemoryGb = isMinimal ? RoundDown(totalMemoryGb, MinimalSystemMemoryStepGb) : totalMemoryGb,
            ProcessArchitecture = processArchitecture.ToString(),
        };
    }

    internal static int ToWholeGigabytes(ulong bytes) => (int)Math.Round(bytes / (double)Size.GiB);

    internal static int? RoundDown(int? value, int step) => value is { } v ? v / step * step : null;
}

public record GpuAnalyticsInfo
{
    /// <summary>Full model name, or just the vendor in minimal mode.</summary>
    public string? Name { get; init; }

    public int MemoryGb { get; init; }

    /// <summary>NVIDIA compute capability such as "8.9"; null for other vendors.</summary>
    public string? ComputeCapability { get; init; }

    public static GpuAnalyticsInfo FromGpuInfo(GpuInfo gpu, bool minimal)
    {
        var memoryGb = HardwareAnalyticsRequest.ToWholeGigabytes(gpu.MemoryBytes);

        return new GpuAnalyticsInfo
        {
            Name = minimal ? GetVendor(gpu) : gpu.Name,
            MemoryGb = minimal
                ? HardwareAnalyticsRequest.RoundDown(
                    memoryGb,
                    HardwareAnalyticsRequest.MinimalGpuMemoryStepGb
                ) ?? 0
                : memoryGb,
            ComputeCapability = gpu.ComputeCapability,
        };
    }

    internal static string GetVendor(GpuInfo gpu)
    {
        if (gpu.IsNvidia)
            return "NVIDIA";
        if (gpu.IsAmd)
            return "AMD";
        if (gpu.IsIntel)
            return "Intel";
        if (gpu.Name?.Contains("apple", StringComparison.OrdinalIgnoreCase) is true)
            return "Apple";

        return "Other";
    }
}
