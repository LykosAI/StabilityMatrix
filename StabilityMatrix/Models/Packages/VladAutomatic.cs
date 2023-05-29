using System;
using System.Collections.Generic;
using StabilityMatrix.Api;
using StabilityMatrix.Helper;

namespace StabilityMatrix.Models.Packages;

public class VladAutomatic : BaseGitPackage
{
    public override string Name => "automatic";
    public override string DisplayName { get; set; } = "SD.Next Web UI";
    public override string Author => "vladmandic";
    public override string LaunchCommand => "launch.py";
    public override string DefaultLaunchArguments => $"{GetVramOption()} {GetXformersOption()}";

    public VladAutomatic(IGithubApi githubApi, ISettingsManager settingsManager) : base(githubApi, settingsManager)
    {
    }

    public override List<LaunchOptionDefinition> LaunchOptions => new()
    {
        new()
        {
            Name = "API",
            Options = new() { "--api" }
        },
        new()
        {
            Name = "VRAM",
            Options = new() { "--lowvram", "--medvram" }
        },
        new()
        {
            Name = "Xformers",
            Options = new() { "--xformers" }
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
