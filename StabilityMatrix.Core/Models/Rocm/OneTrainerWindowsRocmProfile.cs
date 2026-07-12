using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for OneTrainer.
/// Python 3.12 only - required by compatible bitsandbytes wheel.
/// </summary>
public class OneTrainerWindowsRocmProfile : RocmPackageProfile
{
    public static RocmPackageProfile Default { get; } = new OneTrainerWindowsRocmProfile();

    // Restores flop counter functionality requiring triton module
    private const string TritonWindowsPackage = "triton-windows";

    // Replace upstream bitsandbytes with ROCm-aware bitsandbytes for ROCm Technical Preview on Windows
    private const string BitsAndBytesWheelUrl =
        "https://github.com/0xDELUXA/bitsandbytes_win_rocm/releases/download/0.50.0.dev0-py3-rocm7-win_amd64_all/bitsandbytes-0.50.0.dev0-cp312-cp312-win_amd64.whl";

    public static RocmPackageProfile CreateInstallProfile(PyVersion pyVersion)
    {
        if (pyVersion.Major == 3 && pyVersion.Minor == 12)
        {
            return new RocmPackageProfile
            {
                InstallConfig = new PipInstallConfig
                {
                    PostTorchInstallPipArgs = [TritonWindowsPackage, BitsAndBytesWheelUrl],
                },
            };
        }

        return new RocmPackageProfile
        {
            InstallConfig = new PipInstallConfig { PostTorchInstallPipArgs = [TritonWindowsPackage] },
        };
    }
}
