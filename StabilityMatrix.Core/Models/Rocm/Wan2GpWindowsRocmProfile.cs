using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for Wan2GP.
/// </summary>
public class Wan2GpWindowsRocmProfile : RocmPackageProfile
{
    public Wan2GpWindowsRocmProfile()
    {
        InstallConfig = new PipInstallConfig
        {
            RequirementsFilePaths = ["requirements.txt"],
            UpgradePackages = true,
            PostTorchInstallPipArgs = ["hf-xet", "setuptools<70.0.0", "numpy==1.26.4"],
        };
    }

    public static RocmPackageProfile Default { get; } = new Wan2GpWindowsRocmProfile();
}
