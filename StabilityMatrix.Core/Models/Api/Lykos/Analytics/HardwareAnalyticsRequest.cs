using System.Runtime.InteropServices;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;

namespace StabilityMatrix.Core.Models.Api.Lykos.Analytics;

/// <summary>
/// Hardware summary: GPUs, system memory, CPU architecture and NVIDIA driver version. Memory
/// sizes are rounded to whole gigabytes.
/// </summary>
public class HardwareAnalyticsRequest : AnalyticsRequest
{
    public override string Type { get; set; } = "hardware";

    public IReadOnlyList<GpuAnalyticsInfo> Gpus { get; set; } = [];

    public string? NvidiaDriverVersion { get; set; }

    public int? TotalMemoryGb { get; set; }

    public string? ProcessArchitecture { get; set; }

    /// <summary>
    /// Builds the request from the running machine. Probes GPUs, which can take a moment on the
    /// first call, so callers should stay off the UI thread.
    /// </summary>
    public static HardwareAnalyticsRequest FromCurrentSystem()
    {
        var gpus = HardwareHelper.IterGpuInfo().ToList();
        var driverVersion = gpus.Any(gpu => gpu.IsNvidia) ? HardwareHelper.GetNvidiaDriverVersion() : null;
        MemoryInfo? memory = HardwareHelper.TryGetMemoryInfo(out var memoryInfo) ? memoryInfo : null;

        return Create(gpus, memory, driverVersion, RuntimeInformation.ProcessArchitecture);
    }

    public static HardwareAnalyticsRequest Create(
        IEnumerable<GpuInfo> gpus,
        MemoryInfo? memory,
        Version? nvidiaDriverVersion,
        Architecture processArchitecture
    )
    {
        var totalMemoryBytes = memory is { } m ? Math.Max(m.TotalInstalledBytes, m.TotalPhysicalBytes) : 0ul;

        return new HardwareAnalyticsRequest
        {
            Gpus = gpus.Select(GpuAnalyticsInfo.FromGpuInfo).ToList(),
            NvidiaDriverVersion = nvidiaDriverVersion?.ToString(),
            TotalMemoryGb = totalMemoryBytes > 0 ? ToWholeGigabytes(totalMemoryBytes) : null,
            ProcessArchitecture = processArchitecture.ToString(),
        };
    }

    internal static int ToWholeGigabytes(ulong bytes) => (int)Math.Round(bytes / (double)Size.GiB);
}

public record GpuAnalyticsInfo
{
    public string? Name { get; init; }

    public int MemoryGb { get; init; }

    /// <summary>NVIDIA compute capability such as "8.9"; null for other vendors.</summary>
    public string? ComputeCapability { get; init; }

    public static GpuAnalyticsInfo FromGpuInfo(GpuInfo gpu) =>
        new()
        {
            Name = gpu.Name,
            MemoryGb = HardwareAnalyticsRequest.ToWholeGigabytes(gpu.MemoryBytes),
            ComputeCapability = gpu.ComputeCapability,
        };
}
