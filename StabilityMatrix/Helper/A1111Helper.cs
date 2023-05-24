using System;

namespace StabilityMatrix.Helper;

public static class A1111Helper
{
    public static string GetVramOption()
    {
        var vramGb = HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024;

        return vramGb switch
        {
            < 4 => "--lowvram",
            < 8 => "--medvram",
            _ => string.Empty
        };
    }
    
    public static string GetXformersOption()
    {
        var gpuName = HardwareHelper.GetGpuChipName();
        if (gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return "--xformers";
        }
        
        return string.Empty;
    }
}
