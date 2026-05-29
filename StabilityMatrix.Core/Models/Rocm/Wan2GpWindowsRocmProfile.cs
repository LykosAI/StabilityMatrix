using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for Wan2GP.
/// </summary>
public static class Wan2GpWindowsRocmProfile
{
    public static RocmPackageProfile Profile { get; } =
        new()
        {
            InstallConfig = new PipInstallConfig
            {
                RequirementsFilePaths = ["requirements.txt"],
                UpgradePackages = true,
                PostTorchInstallPipArgs = ["hf-xet", "setuptools<70.0.0", "numpy==1.26.4"],
            },
        };
}
