using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Hardware.Info;
using Microsoft.Win32;
using NLog;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Helper.HardwareInfo;

public static partial class HardwareHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static IReadOnlyList<GpuInfo>? cachedGpuInfos;
    private static readonly object cachedGpuInfosLock = new();

    private static readonly Lazy<IHardwareInfo> HardwareInfoLazy = new(() => new Hardware.Info.HardwareInfo()
    );

    public static IHardwareInfo HardwareInfo => HardwareInfoLazy.Value;

    private static string RunBashCommand(string command)
    {
        var processInfo = new ProcessStartInfo("bash", "-c \"" + command + "\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };

        var process = Process.Start(processInfo);

        process.WaitForExit();

        var output = process.StandardOutput.ReadToEnd();

        return output;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<GpuInfo> IterGpuInfoWindows()
    {
        const string gpuRegistryKeyPath =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        using var baseKey = Registry.LocalMachine.OpenSubKey(gpuRegistryKeyPath);

        if (baseKey == null)
            yield break;

        var gpuIndex = 0;

        foreach (var subKeyName in baseKey.GetSubKeyNames().Where(k => k.StartsWith("0")))
        {
            using var subKey = baseKey.OpenSubKey(subKeyName);
            if (subKey != null)
            {
                yield return new GpuInfo
                {
                    Index = gpuIndex++,
                    Name = subKey.GetValue("DriverDesc")?.ToString(),
                    MemoryBytes = Convert.ToUInt64(subKey.GetValue("HardwareInformation.qwMemorySize")),
                };
            }
        }
    }

    [SupportedOSPlatform("linux")]
    private static IEnumerable<GpuInfo> IterGpuInfoLinux()
    {
        var output = RunBashCommand("lspci | grep VGA");
        var gpuLines = output.Split("\n");

        var gpuIndex = 0;

        foreach (var line in gpuLines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var gpuId = line.Split(' ')[0]; // The GPU ID is the first part of the line
            var gpuOutput = RunBashCommand($"lspci -v -s {gpuId}");

            ulong memoryBytes = 0;
            string? name = null;

            // Parse output with regex
            var match = Regex.Match(gpuOutput, @"VGA compatible controller: ([^\n]*)");
            if (match.Success)
            {
                name = match.Groups[1].Value.Trim();
            }

            match = Regex.Match(gpuOutput, @"prefetchable\) \[size=(\\d+)M\]");
            if (match.Success)
            {
                memoryBytes = ulong.Parse(match.Groups[1].Value) * 1024 * 1024;
            }

            yield return new GpuInfo
            {
                Index = gpuIndex++,
                Name = name,
                MemoryBytes = memoryBytes,
            };
        }
    }

    [SupportedOSPlatform("macos")]
    private static IEnumerable<GpuInfo> IterGpuInfoMacos()
    {
        HardwareInfo.RefreshVideoControllerList();

        foreach (var (i, videoController) in HardwareInfo.VideoControllerList.Enumerate())
        {
            var gpuMemoryBytes = 0ul;

            // For arm macs, use the shared system memory
            if (Compat.IsArm)
            {
                gpuMemoryBytes = GetMemoryInfoImplGeneric().TotalPhysicalBytes;
            }

            yield return new GpuInfo
            {
                Index = i,
                Name = videoController.Name,
                MemoryBytes = gpuMemoryBytes,
            };
        }
    }

    /// <summary>
    /// Yields GpuInfo for each GPU in the system.
    /// </summary>
    /// <param name="forceRefresh">If true, refreshes cached GPU info.</param>
    public static IEnumerable<GpuInfo> IterGpuInfo(bool forceRefresh = false)
    {
        // Use cached if available
        if (!forceRefresh && cachedGpuInfos is not null)
        {
            return cachedGpuInfos;
        }

        using var _ = CodeTimer.StartDebug();

        lock (cachedGpuInfosLock)
        {
            if (!forceRefresh && cachedGpuInfos is not null)
            {
                return cachedGpuInfos;
            }

            if (Compat.IsWindows)
            {
                try
                {
                    var smi = IterGpuInfoNvidiaSmi()?.ToList();
                    if (smi is null)
                        return cachedGpuInfos = IterGpuInfoWindows().ToList();

                    var newList = smi.Concat(IterGpuInfoWindows().Where(gpu => !gpu.IsNvidia))
                        .Select(
                            (gpu, index) =>
                                new GpuInfo
                                {
                                    Name = gpu.Name,
                                    Index = index,
                                    MemoryBytes = gpu.MemoryBytes,
                                }
                        );

                    return cachedGpuInfos = newList.ToList();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to get GPU info using nvidia-smi, falling back to registry");
                    return cachedGpuInfos = IterGpuInfoWindows().ToList();
                }
            }

            if (Compat.IsLinux)
            {
                return cachedGpuInfos = IterGpuInfoLinux().ToList();
            }

            if (Compat.IsMacOS)
            {
                return cachedGpuInfos = IterGpuInfoMacos().ToList();
            }

            Logger.Error("Unknown OS, returning empty GPU info list");

            return cachedGpuInfos = [];
        }
    }

    public static IEnumerable<GpuInfo>? IterGpuInfoNvidiaSmi()
    {
        using var _ = CodeTimer.StartDebug();

        var psi = new ProcessStartInfo
        {
            FileName = "nvidia-smi",
            UseShellExecute = false,
            Arguments = "--query-gpu name,memory.total,compute_cap --format=csv",
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        var process = Process.Start(psi);
        process?.WaitForExit();
        var stdout = process?.StandardOutput.ReadToEnd();
        var split = stdout?.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var results = split?[1..];

        if (results is null)
            return null;

        var gpuInfos = new List<GpuInfo>();
        for (var index = 0; index < results?.Length; index++)
        {
            var gpu = results[index];
            var datas = gpu.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (datas is not { Length: 3 })
                continue;

            var memory = Regex.Replace(datas[1], @"([A-Z])\w+", "").Trim();

            gpuInfos.Add(
                new GpuInfo
                {
                    Name = datas[0],
                    Index = index,
                    MemoryBytes = Convert.ToUInt64(memory) * Size.MiB,
                    ComputeCapability = datas[2].Trim(),
                }
            );
        }

        return gpuInfos;
    }

    /// <summary>
    /// Return true if the system has at least one Nvidia GPU.
    /// </summary>
    public static bool HasNvidiaGpu()
    {
        return IterGpuInfo().Any(gpu => gpu.IsNvidia);
    }

    public static bool HasBlackwellGpu()
    {
        return IterGpuInfo()
            .Any(gpu => gpu is { IsNvidia: true, Name: not null, ComputeCapabilityValue: >= 12.0m });
    }

    public static bool HasLegacyNvidiaGpu()
    {
        return IterGpuInfo()
            .Any(gpu => gpu is { IsNvidia: true, Name: not null, ComputeCapabilityValue: < 7.5m });
    }

    public static bool HasAmpereOrNewerGpu()
    {
        return IterGpuInfo()
            .Any(gpu => gpu is { IsNvidia: true, Name: not null, ComputeCapabilityValue: >= 8.6m });
    }

    /// <summary>
    /// Return true if the system has at least one AMD GPU.
    /// </summary>
    public static bool HasAmdGpu()
    {
        return IterGpuInfo().Any(gpu => gpu.IsAmd);
    }

    public static bool HasIntelGpu() => IterGpuInfo().Any(gpu => gpu.IsIntel);

    // Set ROCm for default if AMD and Linux
    public static bool PreferRocm() => !HasNvidiaGpu() && HasAmdGpu() && Compat.IsLinux;

    // Set DirectML for default if AMD and Windows
    public static bool PreferDirectMLOrZluda() => !HasNvidiaGpu() && HasAmdGpu() && Compat.IsWindows;

    private static readonly Lazy<bool> IsMemoryInfoAvailableLazy = new(() => TryGetMemoryInfo(out _));
    public static bool IsMemoryInfoAvailable => IsMemoryInfoAvailableLazy.Value;
    public static bool IsLiveMemoryUsageInfoAvailable => Compat.IsWindows && IsMemoryInfoAvailable;

    public static bool TryGetMemoryInfo(out MemoryInfo memoryInfo)
    {
        try
        {
            memoryInfo = GetMemoryInfo();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to get memory info");

            memoryInfo = default;
            return false;
        }
    }

    /// <summary>
    /// Gets the total and available physical memory in bytes.
    /// </summary>
    public static MemoryInfo GetMemoryInfo() =>
        Compat.IsWindows ? GetMemoryInfoImplWindows() : GetMemoryInfoImplGeneric();

    [SupportedOSPlatform("windows")]
    private static MemoryInfo GetMemoryInfoImplWindows()
    {
        var memoryStatus = new Win32MemoryStatusEx();

        if (!GlobalMemoryStatusEx(ref memoryStatus))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!GetPhysicallyInstalledSystemMemory(out var installedMemoryKb))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new MemoryInfo
        {
            TotalInstalledBytes = (ulong)installedMemoryKb * 1024,
            TotalPhysicalBytes = memoryStatus.UllTotalPhys,
            AvailablePhysicalBytes = memoryStatus.UllAvailPhys,
        };
    }

    private static MemoryInfo GetMemoryInfoImplGeneric()
    {
        HardwareInfo.RefreshMemoryStatus();

        // On macos only TotalPhysical is reported
        if (Compat.IsMacOS)
        {
            return new MemoryInfo
            {
                TotalPhysicalBytes = HardwareInfo.MemoryStatus.TotalPhysical,
                TotalInstalledBytes = HardwareInfo.MemoryStatus.TotalPhysical,
            };
        }

        return new MemoryInfo
        {
            TotalPhysicalBytes = HardwareInfo.MemoryStatus.TotalPhysical,
            TotalInstalledBytes = HardwareInfo.MemoryStatus.TotalPhysical,
            AvailablePhysicalBytes = HardwareInfo.MemoryStatus.AvailablePhysical,
        };
    }

    /// <summary>
    /// Gets cpu info
    /// </summary>
    public static Task<CpuInfo> GetCpuInfoAsync() =>
        Compat.IsWindows ? Task.FromResult(GetCpuInfoImplWindows()) : GetCpuInfoImplGenericAsync();

    [SupportedOSPlatform("windows")]
    private static CpuInfo GetCpuInfoImplWindows()
    {
        var info = new CpuInfo();

        using var processorKey = Registry.LocalMachine.OpenSubKey(
            @"Hardware\Description\System\CentralProcessor\0",
            RegistryKeyPermissionCheck.ReadSubTree
        );

        if (processorKey?.GetValue("ProcessorNameString") is string processorName)
        {
            info = info with { ProcessorCaption = processorName.Trim() };
        }

        return info;
    }

    private static Task<CpuInfo> GetCpuInfoImplGenericAsync()
    {
        return Task.Run(() =>
        {
            HardwareInfo.RefreshCPUList();

            if (HardwareInfo.CpuList.FirstOrDefault() is not { } cpu)
            {
                return default;
            }

            var processorCaption = cpu.Caption.Trim();

            // Try name if caption is empty (like on macos)
            if (string.IsNullOrWhiteSpace(processorCaption))
            {
                processorCaption = cpu.Name.Trim();
            }

            return new CpuInfo { ProcessorCaption = processorCaption };
        });
    }

    [SupportedOSPlatform("windows")]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

    [SupportedOSPlatform("windows")]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref Win32MemoryStatusEx lpBuffer);
}
