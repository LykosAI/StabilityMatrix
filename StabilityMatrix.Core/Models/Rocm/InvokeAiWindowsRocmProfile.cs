using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Core.Models.Rocm;

/// <summary>
/// Shared Windows ROCm profile for InvokeAI.
/// Phase 1 keeps InvokeAI install ownership package-local and uses the ROCm helper for compatibility and launch policy.
/// </summary>
public static class InvokeAiWindowsRocmProfile
{
    public static RocmPackageProfile Profile { get; } = new();

    private const string BitsAndBytesWheelUrl =
        "https://github.com/0xDELUXA/bitsandbytes_win_rocm/releases/download/0.50.0.dev0-py3-rocm7-win_amd64_all/bitsandbytes-0.50.0.dev0-cp312-cp312-win_amd64.whl";

    public static RocmPackageProfile CreateInstallProfile(PyVersion pyVersion)
    {
        if (pyVersion.Major == 3 && pyVersion.Minor == 12)
        {
            return new RocmPackageProfile
            {
                InstallConfig = new PipInstallConfig { PostTorchInstallPipArgs = [BitsAndBytesWheelUrl] },
            };
        }

        return Profile;
    }

    public static RocmCompatibilityResult GetCompatibility(IRocmPackageHelper? rocmPackageHelper)
    {
        if (rocmPackageHelper is null)
        {
            return new RocmCompatibilityResult { IsCompatible = false };
        }

        return rocmPackageHelper.GetCompatibility(Profile);
    }

    public static bool HasSupport(IRocmPackageHelper? rocmPackageHelper)
    {
        return GetCompatibility(rocmPackageHelper).IsCompatible;
    }

    public static bool ShouldApplyLaunchEnvironment(
        IRocmPackageHelper? rocmPackageHelper,
        TorchIndex selectedTorchIndex
    )
    {
        if (!Compat.IsWindows || selectedTorchIndex != TorchIndex.Rocm)
        {
            return false;
        }

        return GetCompatibility(rocmPackageHelper).IsCompatible;
    }

    public static IReadOnlyDictionary<string, string> BuildLaunchEnvironment(
        IRocmPackageHelper? rocmPackageHelper
    )
    {
        return rocmPackageHelper?.BuildLaunchEnvironment(Profile) ?? new Dictionary<string, string>();
    }

    public static IReadOnlyList<string> GetLaunchNoticeLines(
        IRocmPackageHelper? rocmPackageHelper,
        TorchIndex selectedTorchIndex
    )
    {
        return ShouldApplyLaunchEnvironment(rocmPackageHelper, selectedTorchIndex)
            ? rocmPackageHelper?.GetWindowsLaunchNoticeLines() ?? []
            : [];
    }
}
