using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Win32;

namespace StabilityMatrix.Helper;

public static class HardwareHelper
{
    private const string GpuRegistryKeyPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static ulong GetGpuMemoryBytes()
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", false);
        if (key == null)
        {
            return 0;
        }

        var vram = key.GetValue("HardwareInformation.qwMemorySize");
        var vramLong = Convert.ToUInt64(vram);
        return vramLong;
    }

    public static string GetGpuChipName()
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", false);
        if (key == null)
        {
            return "Unknown";
        }

        var gpuName = key.GetValue("HardwareInformation.ChipType");
        return gpuName?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Yields GpuInfo for each GPU in the system.
    /// </summary>
    public static IEnumerable<GpuInfo> IterGpuInfo()
    {
        using var baseKey = Registry.LocalMachine.OpenSubKey(GpuRegistryKeyPath);
        if (baseKey == null)
        {
            yield break;
        }

        foreach (var subKeyName in baseKey.GetSubKeyNames().Where(k => k.StartsWith("0")))
        {
            using var subKey = baseKey.OpenSubKey(subKeyName);
            if (subKey != null)
            {
                yield return new GpuInfo
                {
                    Name = subKey.GetValue("DriverDesc")?.ToString(),
                    MemoryBytes = Convert.ToUInt64(subKey.GetValue("HardwareInformation.qwMemorySize")),
                };
            }
        }
    }
    
    /// <summary>
    /// Return true if the system has at least one Nvidia GPU.
    /// </summary>
    public static bool HasNvidiaGpu()
    {
        return IterGpuInfo().Any(gpu => gpu.Name?.ToLowerInvariant().Contains("nvidia") ?? false);
    }
}

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class Size
{
    public const ulong KiB = 1024;
    public const ulong MiB = KiB * 1024;
    public const ulong GiB = MiB * 1024;
}

public enum Level
{
    Unknown,
    Low,
    Medium,
    High
}

public record GpuInfo
{
    public string? Name { get; init; } = string.Empty;
    public ulong? MemoryBytes { get; init; }
    public Level? MemoryLevel => MemoryBytes switch
    {
        <= 0 => Level.Unknown,
        < 4 * Size.GiB => Level.Low,
        < 8 * Size.GiB => Level.Medium,
        _ => Level.High
    };
}
