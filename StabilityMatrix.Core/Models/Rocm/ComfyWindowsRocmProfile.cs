namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for Comfy backends launched either directly by Stability Matrix or indirectly via SwarmUI.
/// </summary>
public static class ComfyWindowsRocmProfile
{
    public static RocmPackageProfile Profile { get; } =
        new()
        {
            ExtraInstallPipArgs = ["numpy<2"],
            PostInstallPipArgs = ["typing-extensions>=4.15.0"],
            UpgradePackages = true,
            ExtraEnvironmentFactory = BuildEnvironment,
        };

    private static IReadOnlyDictionary<string, string> BuildEnvironment(RocmRuntimeContext runtimeContext)
    {
        return WindowsRocmSupport.IsModernArchitecture(runtimeContext.RuntimeGfxArch)
            ? new Dictionary<string, string> { ["COMFYUI_ENABLE_MIOPEN"] = "1" }
            : new Dictionary<string, string>();
    }
}
