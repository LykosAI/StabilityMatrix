using StabilityMatrix.Core.Models.Packages;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for OneTrainer.
/// </summary>
public class OneTrainerWindowsRocmProfile : RocmPackageProfile
{
    public static RocmPackageProfile Default { get; } = new OneTrainerWindowsRocmProfile();

    // Restores flop counter functionality requiring triton module
    private const string TritonWindowsPackage = "triton-windows";

    // bitsandbytes is deliberately not installed here: OneTrainer pins bnb 0.49.x, but the only
    // Windows ROCm wheels for current ROCm (>= 7.13) are 0.50-based, which removed optimizer
    // arguments OneTrainer still passes (block_wise / percentile_clipping), so 8-bit optimizers
    // crash at startup. Re-enable once upstream OneTrainer supports bnb 0.50.
    public static RocmPackageProfile CreateInstallProfile()
    {
        return new RocmPackageProfile
        {
            InstallConfig = new PipInstallConfig { PostTorchInstallPipArgs = [TritonWindowsPackage] },
        };
    }
}
