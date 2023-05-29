using System;
using System.Collections.Generic;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Models.Packages;

public class A3WebUI : BaseGitPackage
{
    public override string Name => "stable-diffusion-webui";
    public override string DisplayName { get; set; } = "stable-diffusion-webui";
    public override string Author => "AUTOMATIC1111";
    public override string LaunchCommand => "launch.py";
    public override string DefaultLaunchArguments => $"{GetVramOption()} {GetXformersOption()}";

    public A3WebUI(IGithubApi githubApi, ISettingsManager settingsManager) : base(githubApi, settingsManager)
    {
    }

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new LaunchOptionDefinition
        {
            Name = "API",
            Options = new List<string> { "--api" }
        },
        new LaunchOptionDefinition
        {
            Name = "VRAM",
            Options = new List<string> { "--lowvram", "--medvram" }
        },
        new LaunchOptionDefinition
        {
            Name = "Xformers",
            Options = new List<string> { "--xformers" }
        }
    };

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
