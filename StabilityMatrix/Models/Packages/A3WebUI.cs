using System;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Models.Packages;

public class A3WebUI: BasePackage
{
    public override string Name => "stable-diffusion-webui";
    public override string DisplayName => "Stable Diffusion WebUI";
    public override string Author => "AUTOMATIC1111";
    public override string GithubUrl => "https://github.com/AUTOMATIC1111/stable-diffusion-webui";
    public string CommandLineArgs => $"{GetVramOption()} {GetXformersOption()}";
    
    private static string GetVramOption()
    {
        var vramGb = HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024;

        return vramGb switch
        {
            < 4 => "--lowvram",
            < 8 => "--medvram",
            _ => string.Empty
        };
    }
    
    private static string GetXformersOption()
    {
        var gpuName = HardwareHelper.GetGpuChipName();
        return gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "--xformers" : string.Empty;
    }
}
