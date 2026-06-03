using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for Comfy backends launched either directly by Stability Matrix or indirectly via SwarmUI.
/// </summary>
public class ComfyWindowsRocmProfile : RocmPackageProfile
{
    public ComfyWindowsRocmProfile()
    {
        InstallConfig = new PipInstallConfig
        {
            RequirementsFilePaths = ["requirements.txt"],
            ExtraPipArgs = ["numpy<2"],
            PostInstallPipArgs = ["typing-extensions>=4.15.0"],
            UpgradePackages = true,
        };

        ExtraEnvironmentFactory = BuildEnvironment;
    }

    private IReadOnlyDictionary<string, string> BuildEnvironment(RocmRuntimeContext runtimeContext)
    {
        return WindowsRocmSupport.IsModernArchitecture(runtimeContext.RuntimeGfxArch)
            ? new Dictionary<string, string> { ["COMFYUI_ENABLE_MIOPEN"] = "1" }
            : new Dictionary<string, string>();
    }

    public static RocmPackageProfile Default { get; } = new ComfyWindowsRocmProfile();
}
