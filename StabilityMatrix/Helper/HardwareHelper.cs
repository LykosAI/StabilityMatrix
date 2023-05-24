using System;
using Microsoft.Win32;

namespace StabilityMatrix.Helper;

public class HardwareHelper
{
    public static ulong GetGpuMemoryBytes()
    {
        var registry = Registry.LocalMachine;
        var key = registry.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", false);
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
        var key = registry.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", false);
        if (key == null)
        {
            return "Unknown";
        }
        
        var gpuName = key.GetValue("HardwareInformation.ChipType");
        return gpuName?.ToString() ?? "Unknown";
    }
}
