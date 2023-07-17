using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StabilityMatrix.Core.Helper;

public static partial class HardwareHelper
{
    private static IReadOnlyList<GpuInfo>? cachedGpuInfos;

    private static string RunBashCommand(string command)
    {
        var processInfo = new ProcessStartInfo("bash", "-c \"" + command + "\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true
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
        
        if (baseKey == null) yield break;

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
    
    [SupportedOSPlatform("linux")]
    private static IEnumerable<GpuInfo> IterGpuInfoLinux()
    {
        var output = RunBashCommand("lspci | grep VGA");
        var gpuLines = output.Split("\n");

        foreach (var line in gpuLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

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

            match = MyRegex().Match(gpuOutput);
            if (match.Success)
            {
                memoryBytes = ulong.Parse(match.Groups[1].Value) * 1024 * 1024;
            }

            yield return new GpuInfo { Name = name, MemoryBytes = memoryBytes };
        }
    }
    
    /// <summary>
    /// Yields GpuInfo for each GPU in the system.
    /// </summary>
    public static IEnumerable<GpuInfo> IterGpuInfo()
    {
        if (Compat.IsWindows)
        {
            return IterGpuInfoWindows();
        }
        else if (Compat.IsLinux)
        {
            // Since this requires shell commands, fetch cached value if available.
            if (cachedGpuInfos is not null)
            {
                return cachedGpuInfos;
            }
            
            // No cache, fetch and cache.
            cachedGpuInfos = IterGpuInfoLinux().ToList();
            return cachedGpuInfos;
        }
        // TODO: Implement for macOS
        return Enumerable.Empty<GpuInfo>();
    }
    
    /// <summary>
    /// Return true if the system has at least one Nvidia GPU.
    /// </summary>
    public static bool HasNvidiaGpu()
    {
        return IterGpuInfo().Any(gpu => gpu.IsNvidia);
    }
    
    /// <summary>
    /// Return true if the system has at least one AMD GPU.
    /// </summary>
    public static bool HasAmdGpu()
    {
        return IterGpuInfo().Any(gpu => gpu.IsAmd);
    }

    [GeneratedRegex("prefetchable\\) \\[size=(\\d+)M\\]")]
    private static partial Regex MyRegex();
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
    public ulong MemoryBytes { get; init; }
    public Level? MemoryLevel => MemoryBytes switch
    {
        <= 0 => Level.Unknown,
        < 4 * Size.GiB => Level.Low,
        < 8 * Size.GiB => Level.Medium,
        _ => Level.High
    };
    
    public bool IsNvidia => Name?.ToLowerInvariant().Contains("nvidia") ?? false;
    public bool IsAmd => Name?.ToLowerInvariant().Contains("amd") ?? false;
}
