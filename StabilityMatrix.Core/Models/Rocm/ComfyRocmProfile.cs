using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared ROCm profile for Comfy backends launched either directly by Stability Matrix or indirectly via SwarmUI.
/// </summary>
public class ComfyRocmProfile : RocmPackageProfile
{
    public ComfyRocmProfile()
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
        return RocmSupport.IsModernArchitecture(runtimeContext.RuntimeGfxArch)
            ? new Dictionary<string, string> { ["COMFYUI_ENABLE_MIOPEN"] = "1" }
            : new Dictionary<string, string>();
    }

    public static RocmPackageProfile Default { get; } = new ComfyRocmProfile();
}
