using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for reForge.
/// Static ROCm package policy lives here while install-instance-specific requirement discovery stays in the package.
/// </summary>
public static class ReforgeWindowsRocmProfile
{
    // reForge currently pins accelerate 0.21.0, but 0.22.0 avoids the import path that breaks on Windows ROCm torch builds.
    public const string WindowsRocmAccelerateVersion = "0.22.0";

    public static RocmPackageProfile Profile { get; } = CreateProfile([]);

    public static RocmPackageProfile CreateProfile(IEnumerable<string> requirementsFilePaths)
    {
        return new RocmPackageProfile
        {
            InstallConfig = new PipInstallConfig
            {
                PrePipInstallArgs = ["joblib"],
                RequirementsFilePaths = [.. requirementsFilePaths],
                ExtraPipArgs =
                [
                    "https://github.com/openai/CLIP/archive/d50d76daa670286dd6cacf3bcd80b5e4823fc8e1.zip",
                ],
                PostInstallPipArgs =
                [
                    "numpy==1.26.4",
                    "setuptools==69.5.1",
                    $"accelerate=={WindowsRocmAccelerateVersion}",
                ],
                PostTorchInstallPipArgs =
                [
                    "--index-url",
                    "https://pypi.org/simple",
                    "--force-reinstall",
                    "setuptools==69.5.1",
                ],
                UpgradePackages = true,
            },
        };
    }

    public static RocmCompatibilityResult GetCompatibility(IRocmPackageHelper? rocmPackageHelper)
    {
        if (rocmPackageHelper is null)
        {
            return new RocmCompatibilityResult { IsCompatible = false };
        }

        return rocmPackageHelper.GetCompatibility(Profile);
    }
}
